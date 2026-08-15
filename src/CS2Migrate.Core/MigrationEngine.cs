using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CS2Migrate.Core.Models;

namespace CS2Migrate.Core;

public sealed class MigrationEngine(IProcessInspector? processInspector = null)
{
    private readonly IProcessInspector _processInspector = processInspector ?? new ProcessInspector();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public MigrationPreview Preview(MigrationRequest request)
    {
        ValidateRequest(request, checkProcesses: false);
        var sourceFiles = ConfigCatalog.FindFiles(request.Source.ConfigDirectory, request.Categories);
        var files = sourceFiles.Select(file => new MigrationPreviewFile(
            file.Name,
            file.Category,
            file.Length,
            File.Exists(Path.Combine(request.Target.ConfigDirectory, file.Name)))).ToArray();
        return new MigrationPreview(files, files.Sum(file => file.Length));
    }

    public async Task<MigrationResult> MigrateAsync(
        MigrationRequest request,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request, checkProcesses: true);
        var sourceFiles = ConfigCatalog.FindFiles(request.Source.ConfigDirectory, request.Categories);
        if (sourceFiles.Count == 0)
        {
            throw new MigrationException("No matching CS2 settings were found in the source account.");
        }

        var operationId = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..31];
        var targetLocalDirectory = Directory.GetParent(request.Target.ConfigDirectory)?.FullName
            ?? throw new MigrationException("The target CS2 config path is invalid.");
        Directory.CreateDirectory(targetLocalDirectory);
        var stagingDirectory = Path.Combine(targetLocalDirectory, $".cs2migrate-stage-{operationId}");
        var backupDirectory = Path.Combine(
            Path.GetFullPath(request.BackupRoot),
            request.Target.AccountId.ToString(),
            operationId);
        var backupFilesDirectory = Path.Combine(backupDirectory, "files");
        var snapshotConfigDirectory = Path.Combine(
            backupDirectory,
            "snapshot-userdata",
            SteamConstants.Cs2AppId,
            "local",
            "cfg");
        var manifestFiles = new List<BackupManifestFile>();
        var committed = new List<(ConfigFile Source, bool TargetExisted)>();

        try
        {
            Directory.CreateDirectory(stagingDirectory);
            Directory.CreateDirectory(backupFilesDirectory);
            Directory.CreateDirectory(snapshotConfigDirectory);

            for (var index = 0; index < sourceFiles.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var source = sourceFiles[index];
                progress?.Report(new MigrationProgress("Staging", source.Name, index, sourceFiles.Count));
                var stagedPath = Path.Combine(stagingDirectory, source.Name);
                await CopyFileAsync(source.FullPath, stagedPath, cancellationToken);
                var sourceHash = await ComputeSha256Async(source.FullPath, cancellationToken);
                var stagedHash = await ComputeSha256Async(stagedPath, cancellationToken);
                if (!string.Equals(sourceHash, stagedHash, StringComparison.Ordinal))
                {
                    throw new MigrationException($"Integrity verification failed while staging '{source.Name}'.");
                }

                var snapshotPath = Path.Combine(snapshotConfigDirectory, source.Name);
                await CopyFileAsync(stagedPath, snapshotPath, cancellationToken);
                var snapshotHash = await ComputeSha256Async(snapshotPath, cancellationToken);
                if (!string.Equals(sourceHash, snapshotHash, StringComparison.Ordinal))
                {
                    throw new MigrationException($"Integrity verification failed while sealing '{source.Name}'.");
                }

                var targetPath = Path.Combine(request.Target.ConfigDirectory, source.Name);
                var targetExists = File.Exists(targetPath);
                string? targetHash = null;
                if (targetExists)
                {
                    var backupPath = Path.Combine(backupFilesDirectory, source.Name);
                    await CopyFileAsync(targetPath, backupPath, cancellationToken);
                    targetHash = await ComputeSha256Async(targetPath, cancellationToken);
                    var backupHash = await ComputeSha256Async(backupPath, cancellationToken);
                    if (!string.Equals(targetHash, backupHash, StringComparison.Ordinal))
                    {
                        throw new MigrationException($"Integrity verification failed while backing up '{source.Name}'.");
                    }
                }

                manifestFiles.Add(new BackupManifestFile(
                    source.Name,
                    source.Category.ToString(),
                    sourceHash,
                    targetExists,
                    targetHash));
            }

            var manifest = new BackupManifest(
                1,
                DateTimeOffset.UtcNow,
                request.Source.AccountId.ToString(),
                request.Source.SteamId64.ToString(),
                request.Target.AccountId.ToString(),
                request.Target.SteamId64.ToString(),
                request.Categories.ToString(),
                request.Purpose.ToString(),
                manifestFiles);
            await File.WriteAllTextAsync(
                Path.Combine(backupDirectory, "manifest.json"),
                JsonSerializer.Serialize(manifest, JsonOptions),
                cancellationToken);

            var blockers = _processInspector.GetBlockingProcesses();
            if (blockers.Count > 0)
            {
                throw new MigrationException($"Close {string.Join(" and ", blockers)} before migrating settings.");
            }

            for (var index = 0; index < sourceFiles.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var targetPath = Path.Combine(request.Target.ConfigDirectory, sourceFiles[index].Name);
                var captured = manifestFiles[index];
                if (captured.TargetExisted)
                {
                    if (!File.Exists(targetPath) ||
                        !string.Equals(
                            await ComputeSha256Async(targetPath, cancellationToken),
                            captured.TargetSha256,
                            StringComparison.Ordinal))
                    {
                        throw new MigrationException("A target setting changed while the migration was being prepared. Refresh and try again.");
                    }
                }
                else if (File.Exists(targetPath))
                {
                    throw new MigrationException("A target setting changed while the migration was being prepared. Refresh and try again.");
                }
            }

            Directory.CreateDirectory(request.Target.ConfigDirectory);
            for (var index = 0; index < sourceFiles.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var source = sourceFiles[index];
                progress?.Report(new MigrationProgress("Applying", source.Name, index, sourceFiles.Count));
                var targetPath = Path.Combine(request.Target.ConfigDirectory, source.Name);
                File.Move(Path.Combine(stagingDirectory, source.Name), targetPath, overwrite: true);
                committed.Add((source, manifestFiles[index].TargetExisted));
                File.SetLastWriteTimeUtc(targetPath, DateTime.UtcNow.AddTicks(index));
            }

            for (var index = 0; index < sourceFiles.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var source = sourceFiles[index];
                progress?.Report(new MigrationProgress("Verifying", source.Name, index, sourceFiles.Count));
                var targetHash = await ComputeSha256Async(
                    Path.Combine(request.Target.ConfigDirectory, source.Name),
                    cancellationToken);
                var expectedHash = manifestFiles[index].SourceSha256;
                if (!string.Equals(targetHash, expectedHash, StringComparison.Ordinal))
                {
                    throw new MigrationException($"Integrity verification failed after writing '{source.Name}'.");
                }
            }

            await File.WriteAllTextAsync(
                Path.Combine(backupDirectory, "completed.txt"),
                $"Migration completed successfully at {DateTimeOffset.UtcNow:O}{Environment.NewLine}",
                cancellationToken);
            progress?.Report(new MigrationProgress("Complete", "Settings migrated safely", sourceFiles.Count, sourceFiles.Count));

            return new MigrationResult(
                backupDirectory,
                sourceFiles.Count,
                sourceFiles.Sum(file => file.Length),
                sourceFiles.Select(file => file.Name).ToArray());
        }
        catch (OperationCanceledException)
        {
            RollBack(committed, request.Target.ConfigDirectory, backupFilesDirectory);
            throw;
        }
        catch (Exception exception) when (exception is not MigrationException)
        {
            RollBack(committed, request.Target.ConfigDirectory, backupFilesDirectory);
            throw new MigrationException("The migration failed. Any changed files were rolled back.", exception);
        }
        catch (MigrationException)
        {
            RollBack(committed, request.Target.ConfigDirectory, backupFilesDirectory);
            throw;
        }
        finally
        {
            TryDeleteDirectory(stagingDirectory);
        }
    }

    private void ValidateRequest(MigrationRequest request, bool checkProcesses)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Source.AccountId == request.Target.AccountId ||
            PathsEqual(request.Source.UserDataDirectory, request.Target.UserDataDirectory))
        {
            throw new MigrationException("Choose two different Steam accounts.");
        }

        if (request.Categories == MigrationCategory.None ||
            (request.Categories & ~MigrationCategory.AllPortable) != 0)
        {
            throw new MigrationException("Choose at least one supported settings category.");
        }

        ValidateAccountPath(request.Source);
        ValidateAccountPath(request.Target);

        if (!Directory.Exists(request.Source.ConfigDirectory))
        {
            throw new MigrationException("The source account does not have a CS2 config folder yet.");
        }

        if (checkProcesses)
        {
            var blockers = _processInspector.GetBlockingProcesses();
            if (blockers.Count > 0)
            {
                throw new MigrationException($"Close {string.Join(" and ", blockers)} before migrating settings.");
            }
        }
    }

    private static void ValidateAccountPath(SteamAccount account)
    {
        var expected = Path.GetFullPath(Path.Combine(
            account.UserDataDirectory,
            SteamConstants.Cs2AppId,
            "local",
            "cfg"));
        var actual = Path.GetFullPath(account.ConfigDirectory);
        if (!PathsEqual(expected, actual))
        {
            throw new MigrationException("A selected account contains an unexpected CS2 config path.");
        }

        var userData = new DirectoryInfo(account.UserDataDirectory);
        if (userData.Exists && userData.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new MigrationException("Linked Steam userdata folders are not supported for safety.");
        }

        var config = new DirectoryInfo(actual);
        if (config.Exists && config.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new MigrationException("Linked CS2 config folders are not supported for safety.");
        }
    }

    private static bool PathsEqual(string first, string second) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static async Task CopyFileAsync(string source, string destination, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await input.CopyToAsync(output, cancellationToken);
        await output.FlushAsync(cancellationToken);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void RollBack(
        IEnumerable<(ConfigFile Source, bool TargetExisted)> committed,
        string targetDirectory,
        string backupFilesDirectory)
    {
        foreach (var (source, targetExisted) in committed.Reverse())
        {
            try
            {
                var targetPath = Path.Combine(targetDirectory, source.Name);
                if (targetExisted)
                {
                    File.Copy(Path.Combine(backupFilesDirectory, source.Name), targetPath, overwrite: true);
                }
                else if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }
            }
            catch (IOException)
            {
                // Keep the external backup intact so recovery remains possible.
            }
            catch (UnauthorizedAccessException)
            {
                // Keep the external backup intact so recovery remains possible.
            }
        }
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
        catch (IOException)
        {
            // A stale staging folder is harmless and intentionally not force-deleted.
        }
        catch (UnauthorizedAccessException)
        {
            // A stale staging folder is harmless and intentionally not force-deleted.
        }
    }
}
