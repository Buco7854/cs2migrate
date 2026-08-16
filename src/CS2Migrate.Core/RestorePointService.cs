using System.Globalization;
using System.Security.Cryptography;
using CS2Migrate.Core.Models;

namespace CS2Migrate.Core;

/// <summary>
/// Reads the backup folder as a version history. Every operation the app performs archives the
/// files it was about to change, so each archive is a point in time this account can be taken
/// back to — whole, or one file at a time.
/// </summary>
public sealed class RestorePointService(IProcessInspector? processInspector = null)
{
    private readonly IProcessInspector _processInspector = processInspector ?? new ProcessInspector();

    /// <summary>Every restorable point for an account, newest first.</summary>
    public IReadOnlyList<RestorePoint> FindRestorePoints(SteamAccount account, string backupRoot)
    {
        ArgumentNullException.ThrowIfNull(account);
        var accountRoot = Path.Combine(backupRoot, account.AccountId.ToString(CultureInfo.InvariantCulture));
        if (!Directory.Exists(accountRoot))
        {
            return [];
        }

        // Sorted on the recorded timestamp rather than the folder name: snapshots and
        // migrations use different name prefixes, so names do not order reliably.
        var points = new List<RestorePoint>();
        foreach (var archive in new DirectoryInfo(accountRoot).EnumerateDirectories())
        {
            var manifest = AccountBackupService.TryLoadCompletedManifest(archive.FullName);
            if (manifest is null ||
                !string.Equals(
                    manifest.TargetAccountId,
                    account.AccountId.ToString(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal) ||
                !Enum.TryParse<MigrationPurpose>(manifest.Purpose, out var purpose))
            {
                continue;
            }

            var files = ReadRestorableFiles(archive.FullName, manifest, purpose);
            if (files.Count > 0)
            {
                points.Add(new RestorePoint(archive.FullName, manifest.CreatedUtc, KindOf(purpose), files));
            }
        }

        return points
            .OrderByDescending(point => point.CreatedUtc)
            .ThenByDescending(point => point.ArchiveDirectory, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Writes the chosen files back through the migration engine, so the current contents are
    /// archived first and a failure rolls everything back. The restore therefore becomes a
    /// restore point of its own.
    /// </summary>
    public async Task<MigrationResult> RestoreAsync(
        SteamAccount account,
        RestorePoint restorePoint,
        IReadOnlyList<string> fileNames,
        string backupRoot,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(restorePoint);
        ArgumentNullException.ThrowIfNull(fileNames);

        var wanted = restorePoint.Files
            .Where(file => fileNames.Contains(file.Name, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (wanted.Length == 0)
        {
            throw new MigrationException("Choose at least one file to restore.");
        }

        // The engine only reads a real Steam layout, so lay the chosen versions out that way.
        var stagingRoot = Path.Combine(Path.GetTempPath(), $"cs2migrate-restore-{Guid.NewGuid():N}");
        var stagingUserData = Path.Combine(stagingRoot, "userdata");
        var stagingConfig = Path.Combine(stagingUserData, SteamConstants.Cs2AppId, "local", "cfg");

        try
        {
            Directory.CreateDirectory(stagingConfig);
            foreach (var file in wanted)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!File.Exists(file.ArchivePath) ||
                    !string.Equals(ComputeSha256(file.ArchivePath), file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new MigrationException("The selected backup is incomplete or damaged.");
                }

                File.Copy(file.ArchivePath, Path.Combine(stagingConfig, file.Name), overwrite: true);
            }

            var snapshotAccount = new SteamAccount(
                account.AccountId == uint.MaxValue ? uint.MaxValue - 1 : uint.MaxValue,
                SteamConstants.SteamId64Base + (account.AccountId == uint.MaxValue ? uint.MaxValue - 1 : uint.MaxValue),
                account.DisplayName,
                string.Empty,
                stagingUserData,
                stagingConfig,
                account.AvatarPath,
                false,
                restorePoint.CreatedUtc,
                wanted.Length);

            var engine = new MigrationEngine(_processInspector);
            return await engine.MigrateAsync(
                new MigrationRequest(snapshotAccount, account, MigrationCategory.AllPortable, backupRoot),
                progress,
                cancellationToken);
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
        }
    }

    /// <summary>
    /// A snapshot holds the account's own files; a migration or a friend session holds the
    /// files it was about to overwrite. Either way the archive describes how this account
    /// looked before that operation ran.
    /// </summary>
    private static List<RestorePointFile> ReadRestorableFiles(
        string archiveDirectory,
        BackupManifest manifest,
        MigrationPurpose purpose)
    {
        var fromSnapshot = purpose is MigrationPurpose.ManualBackup or MigrationPurpose.SafetyRollback;
        var directory = fromSnapshot
            ? Path.Combine(archiveDirectory, "snapshot-userdata", SteamConstants.Cs2AppId, "local", "cfg")
            : Path.Combine(archiveDirectory, "files");

        var files = new List<RestorePointFile>();
        foreach (var file in manifest.Files)
        {
            // Nothing to put back for a file the operation created rather than replaced.
            if (!fromSnapshot && !file.TargetExisted)
            {
                continue;
            }

            var expectedHash = fromSnapshot ? file.SourceSha256 : file.TargetSha256;
            if (string.IsNullOrWhiteSpace(expectedHash) ||
                string.IsNullOrWhiteSpace(file.Name) ||
                !string.Equals(Path.GetFileName(file.Name), file.Name, StringComparison.Ordinal) ||
                ConfigCatalog.Classify(file.Name) == MigrationCategory.None)
            {
                continue;
            }

            var path = Path.Combine(directory, file.Name);
            try
            {
                if (!File.Exists(path) ||
                    !string.Equals(ComputeSha256(path), expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                files.Add(new RestorePointFile(
                    file.Name,
                    ConfigCatalog.Classify(file.Name),
                    new FileInfo(path).Length,
                    expectedHash,
                    path));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A damaged entry is simply not offered as a restore point.
            }
        }

        return files;
    }

    private static RestorePointKind KindOf(MigrationPurpose purpose) => purpose switch
    {
        MigrationPurpose.ManualBackup => RestorePointKind.ManualBackup,
        MigrationPurpose.SafetyRollback => RestorePointKind.AutomaticSafetyCopy,
        MigrationPurpose.TemporaryFriendSession => RestorePointKind.BeforeFriendSession,
        _ => RestorePointKind.BeforeMigration
    };

    private static string ComputeSha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Only copies of already archived files live here.
        }
    }
}
