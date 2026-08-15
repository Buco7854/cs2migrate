using CS2Migrate.Core.Models;

namespace CS2Migrate.Core;

public static class ConfigCatalog
{
    public static IReadOnlyList<ConfigFile> FindFiles(
        string configDirectory,
        MigrationCategory categories = MigrationCategory.AllPortable)
    {
        if (!Directory.Exists(configDirectory) || categories == MigrationCategory.None)
        {
            return [];
        }

        return new DirectoryInfo(configDirectory)
            .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
            .Where(file => !file.Attributes.HasFlag(FileAttributes.ReparsePoint))
            .Select(file => (File: file, Category: Classify(file.Name)))
            .Where(item => item.Category != MigrationCategory.None && categories.HasFlag(item.Category))
            .Where(item => item.File.Length <= SteamConstants.MaxConfigFileBytes)
            .OrderBy(item => item.Category)
            .ThenBy(item => item.File.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => new ConfigFile(
                item.File.Name,
                item.File.FullName,
                item.Category,
                item.File.Length,
                item.File.LastWriteTimeUtc))
            .ToArray();
    }

    public static MigrationCategory Classify(string fileName)
    {
        var name = fileName.ToLowerInvariant();

        if ((name.StartsWith("cs2_user_convars_", StringComparison.Ordinal) && name.EndsWith(".vcfg", StringComparison.Ordinal)) ||
            name == "config.cfg")
        {
            return MigrationCategory.Gameplay;
        }

        if ((name.StartsWith("cs2_user_keys_", StringComparison.Ordinal) && name.EndsWith(".vcfg", StringComparison.Ordinal)) ||
            (name.StartsWith("user_keys", StringComparison.Ordinal) && name.EndsWith(".vcfg", StringComparison.Ordinal)))
        {
            return MigrationCategory.Keybinds;
        }

        if (name is "cs2_video.txt" or "video.txt")
        {
            return MigrationCategory.Video;
        }

        if (name == "autoexec.cfg")
        {
            return MigrationCategory.Autoexec;
        }

        return MigrationCategory.None;
    }
}
