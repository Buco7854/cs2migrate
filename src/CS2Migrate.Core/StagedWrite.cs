using CS2Migrate.Core.Models;

namespace CS2Migrate.Core;

/// <summary>
/// Writes a set of files into an account through the migration engine, so the current
/// contents are archived first, every copy is verified, and a failure rolls back.
/// </summary>
internal static class StagedWrite
{
    public static async Task<MigrationResult> ApplyAsync(
        SteamAccount account,
        IReadOnlyList<StagedFile> files,
        string backupRoot,
        IProcessInspector processInspector,
        DateTimeOffset capturedUtc,
        IProgress<MigrationProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
            throw new MigrationException("Choose at least one file to restore.");
        }

        // The engine only reads a real Steam layout, so lay the chosen versions out that way.
        var stagingRoot = Path.Combine(Path.GetTempPath(), $"cs2migrate-apply-{Guid.NewGuid():N}");
        var stagingUserData = Path.Combine(stagingRoot, "userdata");
        var stagingConfig = Path.Combine(stagingUserData, SteamConstants.Cs2AppId, "local", "cfg");

        try
        {
            Directory.CreateDirectory(stagingConfig);
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await file.CopyToAsync(Path.Combine(stagingConfig, file.Name), cancellationToken);
            }

            var syntheticId = account.AccountId == uint.MaxValue ? uint.MaxValue - 1 : uint.MaxValue;
            var source = new SteamAccount(
                syntheticId,
                SteamConstants.SteamId64Base + syntheticId,
                account.DisplayName,
                string.Empty,
                stagingUserData,
                stagingConfig,
                account.AvatarPath,
                false,
                capturedUtc,
                files.Count);

            var engine = new MigrationEngine(processInspector);
            return await engine.MigrateAsync(
                new MigrationRequest(source, account, MigrationCategory.AllPortable, backupRoot),
                progress,
                cancellationToken);
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Only copies of files that already exist elsewhere live here.
        }
    }
}

/// <summary>One file to write, wherever its contents happen to come from.</summary>
internal sealed record StagedFile(string Name, Func<string, CancellationToken, Task> CopyTo)
{
    public Task CopyToAsync(string destination, CancellationToken cancellationToken) =>
        CopyTo(destination, cancellationToken);
}
