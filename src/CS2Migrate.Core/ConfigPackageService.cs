using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using CS2Migrate.Core.Models;

namespace CS2Migrate.Core;

/// <summary>
/// Exports an account's settings to a single file that can be kept or handed to someone else,
/// and imports one back into an account.
/// </summary>
public sealed class ConfigPackageService(IProcessInspector? processInspector = null)
{
    public const string FileExtension = ".cs2config";

    private const string ManifestEntry = "package.json";
    private const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly IProcessInspector _processInspector = processInspector ?? new ProcessInspector();

    public async Task<ConfigPackage> ExportAsync(
        SteamAccount account,
        string destinationFile,
        MigrationCategory categories = MigrationCategory.AllPortable,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        var files = ConfigCatalog.FindFiles(account.ConfigDirectory, categories);
        if (files.Count == 0)
        {
            throw new MigrationException("No portable CS2 settings were found for this account.");
        }

        var entries = new List<ConfigPackageEntry>();
        var temporaryFile = destinationFile + ".partial";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destinationFile))!);
            await using (var stream = new FileStream(temporaryFile, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entry = archive.CreateEntry($"files/{file.Name}", CompressionLevel.Optimal);
                    await using (var input = File.OpenRead(file.FullPath))
                    await using (var output = entry.Open())
                    {
                        await input.CopyToAsync(output, cancellationToken);
                    }

                    entries.Add(new ConfigPackageEntry(
                        file.Name,
                        file.Category,
                        file.Length,
                        ComputeSha256(file.FullPath)));
                }

                var manifest = new PackageManifest(
                    SchemaVersion,
                    DateTimeOffset.UtcNow,
                    account.DisplayName,
                    entries.Select(entry => new PackageManifestFile(
                        entry.Name,
                        entry.Category.ToString(),
                        entry.Length,
                        entry.Sha256)).ToArray());

                var manifestEntry = archive.CreateEntry(ManifestEntry, CompressionLevel.Optimal);
                await using var manifestStream = manifestEntry.Open();
                await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions, cancellationToken);
            }

            File.Move(temporaryFile, destinationFile, overwrite: true);
        }
        catch (OperationCanceledException)
        {
            TryDelete(temporaryFile);
            throw;
        }
        catch (MigrationException)
        {
            TryDelete(temporaryFile);
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            TryDelete(temporaryFile);
            throw new MigrationException("The settings file could not be written.", exception);
        }

        return new ConfigPackage(destinationFile, DateTimeOffset.UtcNow, account.DisplayName, entries);
    }

    /// <summary>Reads and validates a package without writing anything.</summary>
    public ConfigPackage Read(string packageFile)
    {
        try
        {
            using var archive = ZipFile.OpenRead(packageFile);
            var manifestEntry = archive.GetEntry(ManifestEntry)
                ?? throw new MigrationException("That file is not a CS2 Migrate settings file.");

            using var manifestStream = manifestEntry.Open();
            var manifest = JsonSerializer.Deserialize<PackageManifest>(manifestStream, JsonOptions);
            if (manifest is not { SchemaVersion: SchemaVersion, Files.Count: > 0 })
            {
                throw new MigrationException("That settings file was written by a newer version, or is damaged.");
            }

            var entries = new List<ConfigPackageEntry>();
            foreach (var file in manifest.Files)
            {
                if (string.IsNullOrWhiteSpace(file.Name) ||
                    !string.Equals(Path.GetFileName(file.Name), file.Name, StringComparison.Ordinal) ||
                    ConfigCatalog.Classify(file.Name) == MigrationCategory.None ||
                    archive.GetEntry($"files/{file.Name}") is null)
                {
                    throw new MigrationException("That settings file is incomplete or damaged.");
                }

                entries.Add(new ConfigPackageEntry(
                    file.Name,
                    ConfigCatalog.Classify(file.Name),
                    file.Length,
                    file.Sha256));
            }

            return new ConfigPackage(packageFile, manifest.CreatedUtc, manifest.AccountName, entries);
        }
        catch (MigrationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                              InvalidDataException or JsonException)
        {
            throw new MigrationException("That file is not a CS2 Migrate settings file.", exception);
        }
    }

    /// <summary>
    /// Writes the chosen files from a package into an account. The account's current contents
    /// are archived first, so an import can be undone from the history.
    /// </summary>
    public async Task<MigrationResult> ImportAsync(
        SteamAccount account,
        ConfigPackage package,
        IReadOnlyList<string> fileNames,
        string backupRoot,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(fileNames);

        var wanted = package.Entries
            .Where(entry => fileNames.Contains(entry.Name, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        using var archive = ZipFile.OpenRead(package.FilePath);
        var staged = new List<StagedFile>();
        foreach (var entry in wanted)
        {
            var zipEntry = archive.GetEntry($"files/{entry.Name}")
                ?? throw new MigrationException("That settings file is incomplete or damaged.");

            staged.Add(new StagedFile(entry.Name, async (destination, token) =>
            {
                await using (var input = zipEntry.Open())
                await using (var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await input.CopyToAsync(output, token);
                }

                if (!string.Equals(ComputeSha256(destination), entry.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new MigrationException("That settings file is incomplete or damaged.");
                }
            }));
        }

        return await StagedWrite.ApplyAsync(
            account,
            staged,
            backupRoot,
            _processInspector,
            package.CreatedUtc,
            progress,
            cancellationToken);
    }

    private static string ComputeSha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A half-written export is never moved into place.
        }
    }

    private sealed record PackageManifest(
        int SchemaVersion,
        DateTimeOffset CreatedUtc,
        string AccountName,
        IReadOnlyList<PackageManifestFile> Files);

    private sealed record PackageManifestFile(string Name, string Category, long Length, string Sha256);
}
