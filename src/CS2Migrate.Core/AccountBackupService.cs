using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using CS2Migrate.Core.Models;

namespace CS2Migrate.Core;

public sealed class AccountBackupService(IProcessInspector? processInspector = null)
{
    private readonly IProcessInspector _processInspector = processInspector ?? new ProcessInspector();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public Task<AccountBackup> CreateManualBackupAsync(
        SteamAccount account,
        string backupRoot,
        CancellationToken cancellationToken = default) =>
        CreateSnapshotAsync(account, backupRoot, MigrationPurpose.ManualBackup, cancellationToken);

    public AccountBackup? FindLatestManualBackup(SteamAccount account, string backupRoot)
    {
        var accountRoot = Path.Combine(backupRoot, account.AccountId.ToString(CultureInfo.InvariantCulture));
        if (!Directory.Exists(accountRoot))
        {
            return null;
        }

        foreach (var archive in new DirectoryInfo(accountRoot)
                     .EnumerateDirectories()
                     .OrderByDescending(directory => directory.Name, StringComparer.Ordinal))
        {
            var manifest = TryLoadCompletedManifest(archive.FullName);
            if (manifest is null ||
                !string.Equals(manifest.Purpose, MigrationPurpose.ManualBackup.ToString(), StringComparison.Ordinal) ||
                !string.Equals(manifest.TargetAccountId, account.AccountId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
            {
                continue;
            }

            var snapshotUserData = Path.Combine(archive.FullName, "snapshot-userdata");
            var snapshotConfig = Path.Combine(snapshotUserData, SteamConstants.Cs2AppId, "local", "cfg");
            if (!ValidateSnapshot(manifest, snapshotConfig))
            {
                continue;
            }

            var snapshotSource = CreateSnapshotAccount(account, snapshotUserData, snapshotConfig, manifest);
            return new AccountBackup(account, snapshotSource, archive.FullName, manifest.CreatedUtc, manifest.Files.Count);
        }

        return null;
    }

    public TemporarySessionRecovery? FindPendingTemporarySession(
        IEnumerable<SteamAccount> accounts,
        string backupRoot)
    {
        foreach (var target in accounts)
        {
            var accountRoot = Path.Combine(backupRoot, target.AccountId.ToString(CultureInfo.InvariantCulture));
            if (!Directory.Exists(accountRoot))
            {
                continue;
            }

            foreach (var archive in new DirectoryInfo(accountRoot)
                         .EnumerateDirectories()
                         .OrderByDescending(directory => directory.Name, StringComparer.Ordinal))
            {
                var manifest = TryLoadCompletedManifest(archive.FullName);
                if (manifest is null ||
                    !string.Equals(manifest.Purpose, MigrationPurpose.TemporaryFriendSession.ToString(), StringComparison.Ordinal) ||
                    !string.Equals(manifest.TargetAccountId, target.AccountId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                {
                    continue;
                }

                if (File.Exists(Path.Combine(archive.FullName, "restored.txt")))
                {
                    break;
                }

                var changed = CountFilesDifferentFromOriginal(manifest, archive.FullName, target.ConfigDirectory);
                if (changed < 0)
                {
                    continue;
                }

                if (changed > 0)
                {
                    return new TemporarySessionRecovery(
                        target,
                        archive.FullName,
                        manifest.CreatedUtc,
                        changed,
                        manifest.Files.Count,
                        manifest.Files.Select(file => file.Name).ToArray());
                }

                break;
            }
        }

        return null;
    }

    public Task<MigrationResult> RestoreManualBackupAsync(
        AccountBackup backup,
        string backupRoot,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var engine = new MigrationEngine(_processInspector);
        return engine.MigrateAsync(
            new MigrationRequest(
                backup.SnapshotSource,
                backup.Account,
                MigrationCategory.AllPortable,
                backupRoot),
            progress,
            cancellationToken);
    }

    public async Task<MigrationResult> RestoreTemporarySessionAsync(
        TemporarySessionRecovery recovery,
        string backupRoot,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureProcessesClosed();
        var manifest = TryLoadCompletedManifest(recovery.ArchiveDirectory)
            ?? throw new MigrationException("The temporary session backup is incomplete or damaged.");
        if (!string.Equals(manifest.Purpose, MigrationPurpose.TemporaryFriendSession.ToString(), StringComparison.Ordinal))
        {
            throw new MigrationException("The selected backup is not a temporary friend session.");
        }

        var safetyBackup = await CreateSnapshotAsync(
            recovery.Target,
            backupRoot,
            MigrationPurpose.SafetyRollback,
            cancellationToken);
        var targetLocal = Directory.GetParent(recovery.Target.ConfigDirectory)?.FullName
            ?? throw new MigrationException("The target CS2 config path is invalid.");
        Directory.CreateDirectory(targetLocal);
        var stagingDirectory = Path.Combine(targetLocal, $".cs2migrate-restore-{Guid.NewGuid():N}");
        var restoreStage = Path.Combine(stagingDirectory, "restore");
        var rollbackStage = Path.Combine(stagingDirectory, "rollback");
        var currentStates = new List<(string Name, bool Existed)>();
        var touched = new List<string>();

        try
        {
            Directory.CreateDirectory(restoreStage);
            Directory.CreateDirectory(rollbackStage);
            Directory.CreateDirectory(recovery.Target.ConfigDirectory);

            for (var index = 0; index < manifest.Files.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var file = manifest.Files[index];
                ValidateManifestFile(file);
                progress?.Report(new MigrationProgress("Staging", file.Name, index, manifest.Files.Count));

                var targetPath = Path.Combine(recovery.Target.ConfigDirectory, file.Name);
                var targetExists = File.Exists(targetPath);
                currentStates.Add((file.Name, targetExists));
                if (targetExists)
                {
                    await CopyFileAsync(targetPath, Path.Combine(rollbackStage, file.Name), cancellationToken);
                }

                if (file.TargetExisted)
                {
                    var originalPath = Path.Combine(recovery.ArchiveDirectory, "files", file.Name);
                    if (!File.Exists(originalPath) ||
                        !string.Equals(ComputeSha256(originalPath), file.TargetSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new MigrationException("The temporary session backup is incomplete or damaged.");
                    }

                    await CopyFileAsync(originalPath, Path.Combine(restoreStage, file.Name), cancellationToken);
                }
            }

            EnsureProcessesClosed();
            for (var index = 0; index < manifest.Files.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var file = manifest.Files[index];
                var targetPath = Path.Combine(recovery.Target.ConfigDirectory, file.Name);
                progress?.Report(new MigrationProgress("Applying", file.Name, index, manifest.Files.Count));
                if (file.TargetExisted)
                {
                    File.Move(Path.Combine(restoreStage, file.Name), targetPath, overwrite: true);
                    touched.Add(file.Name);
                    File.SetLastWriteTimeUtc(targetPath, DateTime.UtcNow.AddTicks(index));
                }
                else if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                    touched.Add(file.Name);
                }
            }

            for (var index = 0; index < manifest.Files.Count; index++)
            {
                var file = manifest.Files[index];
                var targetPath = Path.Combine(recovery.Target.ConfigDirectory, file.Name);
                progress?.Report(new MigrationProgress("Verifying", file.Name, index, manifest.Files.Count));
                if (file.TargetExisted)
                {
                    if (!File.Exists(targetPath) ||
                        !string.Equals(ComputeSha256(targetPath), file.TargetSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new MigrationException("Integrity verification failed while restoring the temporary session.");
                    }
                }
                else if (File.Exists(targetPath))
                {
                    throw new MigrationException("Integrity verification failed while restoring the temporary session.");
                }
            }

            await File.WriteAllTextAsync(
                Path.Combine(recovery.ArchiveDirectory, "restored.txt"),
                $"Original settings restored at {DateTimeOffset.UtcNow:O}{Environment.NewLine}",
                cancellationToken);
            progress?.Report(new MigrationProgress("Complete", "Original settings restored", manifest.Files.Count, manifest.Files.Count));
            return new MigrationResult(
                safetyBackup.ArchiveDirectory,
                manifest.Files.Count,
                manifest.Files.Sum(file =>
                {
                    var path = Path.Combine(recovery.Target.ConfigDirectory, file.Name);
                    return File.Exists(path) ? new FileInfo(path).Length : 0;
                }),
                manifest.Files.Select(file => file.Name).ToArray());
        }
        catch (OperationCanceledException)
        {
            RollBackTemporaryRestore(touched, currentStates, recovery.Target.ConfigDirectory, rollbackStage);
            throw;
        }
        catch (MigrationException)
        {
            RollBackTemporaryRestore(touched, currentStates, recovery.Target.ConfigDirectory, rollbackStage);
            throw;
        }
        catch (Exception exception)
        {
            RollBackTemporaryRestore(touched, currentStates, recovery.Target.ConfigDirectory, rollbackStage);
            throw new MigrationException(
                "The restore failed. Any changed files were rolled back.",
                exception);
        }
        finally
        {
            TryDeleteDirectory(stagingDirectory);
        }
    }

    private async Task<AccountBackup> CreateSnapshotAsync(
        SteamAccount account,
        string backupRoot,
        MigrationPurpose purpose,
        CancellationToken cancellationToken)
    {
        try
        {
            return await CreateSnapshotCoreAsync(account, backupRoot, purpose, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (MigrationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new MigrationException("The backup failed before a verified snapshot could be completed.", exception);
        }
    }

    private async Task<AccountBackup> CreateSnapshotCoreAsync(
        SteamAccount account,
        string backupRoot,
        MigrationPurpose purpose,
        CancellationToken cancellationToken)
    {
        EnsureProcessesClosed();
        var files = ConfigCatalog.FindFiles(account.ConfigDirectory, MigrationCategory.AllPortable);
        if (files.Count == 0 && purpose != MigrationPurpose.SafetyRollback)
        {
            throw new MigrationException("No portable CS2 settings were found for this account.");
        }

        var prefix = purpose == MigrationPurpose.ManualBackup ? "manual" : "safety";
        var operationId = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{prefix}-{Guid.NewGuid():N}"[..39];
        var archiveDirectory = Path.Combine(
            Path.GetFullPath(backupRoot),
            account.AccountId.ToString(CultureInfo.InvariantCulture),
            operationId);
        var snapshotUserData = Path.Combine(archiveDirectory, "snapshot-userdata");
        var snapshotConfig = Path.Combine(snapshotUserData, SteamConstants.Cs2AppId, "local", "cfg");
        Directory.CreateDirectory(snapshotConfig);
        var manifestFiles = new List<BackupManifestFile>();

        for (var index = 0; index < files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = files[index];
            var snapshotPath = Path.Combine(snapshotConfig, file.Name);
            await CopyFileAsync(file.FullPath, snapshotPath, cancellationToken);
            var sourceHash = ComputeSha256(file.FullPath);
            if (!string.Equals(sourceHash, ComputeSha256(snapshotPath), StringComparison.OrdinalIgnoreCase))
            {
                throw new MigrationException($"Integrity verification failed while backing up '{file.Name}'.");
            }

            manifestFiles.Add(new BackupManifestFile(
                file.Name,
                file.Category.ToString(),
                sourceHash,
                true,
                sourceHash));
        }

        var manifest = new BackupManifest(
            1,
            DateTimeOffset.UtcNow,
            account.AccountId.ToString(CultureInfo.InvariantCulture),
            account.SteamId64.ToString(CultureInfo.InvariantCulture),
            account.AccountId.ToString(CultureInfo.InvariantCulture),
            account.SteamId64.ToString(CultureInfo.InvariantCulture),
            MigrationCategory.AllPortable.ToString(),
            purpose.ToString(),
            manifestFiles);
        await File.WriteAllTextAsync(
            Path.Combine(archiveDirectory, "manifest.json"),
            JsonSerializer.Serialize(manifest, JsonOptions),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(archiveDirectory, "completed.txt"),
            $"Backup completed at {DateTimeOffset.UtcNow:O}{Environment.NewLine}",
            cancellationToken);

        return new AccountBackup(
            account,
            CreateSnapshotAccount(account, snapshotUserData, snapshotConfig, manifest),
            archiveDirectory,
            manifest.CreatedUtc,
            manifest.Files.Count);
    }

    private void EnsureProcessesClosed()
    {
        var blockers = _processInspector.GetBlockingProcesses();
        if (blockers.Count > 0)
        {
            throw new MigrationException($"Close {string.Join(" and ", blockers)} before migrating settings.");
        }
    }

    internal static BackupManifest? TryLoadCompletedManifest(string archiveDirectory)
    {
        var manifestPath = Path.Combine(archiveDirectory, "manifest.json");
        if (!File.Exists(manifestPath) || !File.Exists(Path.Combine(archiveDirectory, "completed.txt")))
        {
            return null;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(manifestPath), JsonOptions);
            if (manifest is not { SchemaVersion: 1, Files.Count: > 0 } ||
                !Enum.TryParse<MigrationPurpose>(manifest.Purpose, ignoreCase: false, out _) ||
                manifest.Files.Any(file => string.IsNullOrWhiteSpace(file.Name)) ||
                manifest.Files.Select(file => file.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.Files.Count)
            {
                return null;
            }

            return manifest;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static SteamAccount CreateSnapshotAccount(
        SteamAccount account,
        string snapshotUserData,
        string snapshotConfig,
        BackupManifest manifest)
    {
        var syntheticId = account.AccountId == uint.MaxValue ? uint.MaxValue - 1 : uint.MaxValue;
        return new SteamAccount(
            syntheticId,
            SteamConstants.SteamId64Base + syntheticId,
            account.DisplayName,
            string.Empty,
            snapshotUserData,
            snapshotConfig,
            account.AvatarPath,
            false,
            manifest.CreatedUtc,
            manifest.Files.Count);
    }

    private static bool ValidateSnapshot(BackupManifest manifest, string snapshotConfig)
    {
        foreach (var file in manifest.Files)
        {
            try
            {
                ValidateManifestFile(file);
                var snapshotPath = Path.Combine(snapshotConfig, file.Name);
                if (!File.Exists(snapshotPath) ||
                    !string.Equals(ComputeSha256(snapshotPath), file.SourceSha256, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            catch (MigrationException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        return manifest.Files.Count > 0;
    }

    private static int CountFilesDifferentFromOriginal(
        BackupManifest manifest,
        string archiveDirectory,
        string targetConfig)
    {
        var changed = 0;
        try
        {
            foreach (var file in manifest.Files)
            {
                ValidateManifestFile(file);
                var targetPath = Path.Combine(targetConfig, file.Name);
                if (file.TargetExisted)
                {
                    var originalPath = Path.Combine(archiveDirectory, "files", file.Name);
                    if (!File.Exists(originalPath) ||
                        !string.Equals(ComputeSha256(originalPath), file.TargetSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        return -1;
                    }

                    if (!File.Exists(targetPath) ||
                        !string.Equals(ComputeSha256(targetPath), file.TargetSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        changed++;
                    }
                }
                else if (File.Exists(targetPath))
                {
                    changed++;
                }
            }

            return changed;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or MigrationException)
        {
            return -1;
        }
    }

    private static void ValidateManifestFile(BackupManifestFile file)
    {
        if (string.IsNullOrWhiteSpace(file.Name) ||
            !string.Equals(Path.GetFileName(file.Name), file.Name, StringComparison.Ordinal) ||
            ConfigCatalog.Classify(file.Name) == MigrationCategory.None)
        {
            throw new MigrationException("A backup contains an unsupported file name.");
        }
    }

    private static void RollBackTemporaryRestore(
        IEnumerable<string> touched,
        IReadOnlyList<(string Name, bool Existed)> currentStates,
        string targetDirectory,
        string rollbackDirectory)
    {
        var states = currentStates.ToDictionary(state => state.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var name in touched.Reverse())
        {
            try
            {
                var targetPath = Path.Combine(targetDirectory, name);
                if (states[name].Existed)
                {
                    File.Copy(Path.Combine(rollbackDirectory, name), targetPath, overwrite: true);
                }
                else if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // The permanent safety snapshot remains available for manual recovery.
            }
        }
    }

    private static async Task CopyFileAsync(string source, string destination, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await input.CopyToAsync(output, cancellationToken);
        await output.FlushAsync(cancellationToken);
    }

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
            // The staging directory contains no unique backup data.
        }
    }
}
