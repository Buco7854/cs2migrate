using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using CS2Migrate.Core.Models;

namespace CS2Migrate.Core;

public sealed class CloudRecoveryService
{
    public CloudRecoveryCandidate? FindLatestMismatch(
        IEnumerable<SteamAccount> accounts,
        string backupRoot)
    {
        if (!Directory.Exists(backupRoot))
        {
            return null;
        }

        foreach (var target in accounts)
        {
            var accountBackups = Path.Combine(backupRoot, target.AccountId.ToString(CultureInfo.InvariantCulture));
            if (!Directory.Exists(accountBackups))
            {
                continue;
            }

            foreach (var archive in new DirectoryInfo(accountBackups)
                         .EnumerateDirectories()
                         .OrderByDescending(directory => directory.Name, StringComparer.Ordinal))
            {
                var inspection = TryInspectArchive(target, archive.FullName);
                if (inspection is not null)
                {
                    if (inspection.Candidate is { ChangedFileCount: > 0 } candidate)
                    {
                        return candidate;
                    }

                    if (inspection.SupersedesOlderArchives)
                    {
                        break;
                    }
                }
            }
        }

        return null;
    }

    private static RecoveryInspection? TryInspectArchive(SteamAccount target, string archiveDirectory)
    {
        var manifestPath = Path.Combine(archiveDirectory, "manifest.json");
        var completionPath = Path.Combine(archiveDirectory, "completed.txt");
        var snapshotUserData = Path.Combine(archiveDirectory, "snapshot-userdata");
        var snapshotConfig = Path.Combine(snapshotUserData, SteamConstants.Cs2AppId, "local", "cfg");
        if (!File.Exists(manifestPath) || !File.Exists(completionPath) || !Directory.Exists(snapshotConfig))
        {
            return null;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(manifestPath));
            if (manifest is not { SchemaVersion: 1, Files.Count: > 0 } ||
                manifest.TargetAccountId != target.AccountId.ToString(CultureInfo.InvariantCulture) ||
                !uint.TryParse(manifest.SourceAccountId, NumberStyles.None, CultureInfo.InvariantCulture, out var sourceAccountId) ||
                !ulong.TryParse(manifest.SourceSteamId64, NumberStyles.None, CultureInfo.InvariantCulture, out var sourceSteamId64) ||
                !Enum.TryParse<MigrationPurpose>(manifest.Purpose, ignoreCase: false, out var purpose) ||
                manifest.Files.Any(file => string.IsNullOrWhiteSpace(file.Name)) ||
                manifest.Files.Select(file => file.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.Files.Count)
            {
                return null;
            }

            if (purpose != MigrationPurpose.Standard)
            {
                return new RecoveryInspection(true, null);
            }

            var changed = 0;
            foreach (var file in manifest.Files)
            {
                if (!string.Equals(Path.GetFileName(file.Name), file.Name, StringComparison.Ordinal) ||
                    ConfigCatalog.Classify(file.Name) == MigrationCategory.None)
                {
                    return null;
                }

                var snapshotPath = Path.Combine(snapshotConfig, file.Name);
                var targetPath = Path.Combine(target.ConfigDirectory, file.Name);
                if (!File.Exists(snapshotPath))
                {
                    return null;
                }

                var snapshotHash = ComputeSha256(snapshotPath);
                if (!string.Equals(snapshotHash, file.SourceSha256, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                if (!File.Exists(targetPath) ||
                    !string.Equals(snapshotHash, ComputeSha256(targetPath), StringComparison.OrdinalIgnoreCase))
                {
                    changed++;
                }
            }

            var snapshotSource = new SteamAccount(
                sourceAccountId,
                sourceSteamId64,
                "Saved migration",
                string.Empty,
                snapshotUserData,
                snapshotConfig,
                null,
                false,
                manifest.CreatedUtc,
                manifest.Files.Count);

            return new RecoveryInspection(
                true,
                new CloudRecoveryCandidate(
                    target,
                    snapshotSource,
                    archiveDirectory,
                    changed,
                    manifest.Files.Count));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed record RecoveryInspection(
        bool SupersedesOlderArchives,
        CloudRecoveryCandidate? Candidate);
}
