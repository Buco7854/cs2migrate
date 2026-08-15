using System.Runtime.Versioning;
using Microsoft.Win32;

namespace CS2Migrate.Core;

public static class SteamPathDetector
{
    public static string? Find()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        foreach (var candidate in FindWindowsCandidates())
        {
            if (IsSteamDirectory(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    public static bool IsSteamDirectory(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        File.Exists(Path.Combine(path, "steam.exe")) &&
        Directory.Exists(Path.Combine(path, "userdata"));

    [SupportedOSPlatform("windows")]
    private static IEnumerable<string> FindWindowsCandidates()
    {
        foreach (var (hive, keyPath) in new[]
                 {
                     (Registry.CurrentUser, @"Software\Valve\Steam"),
                     (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam"),
                     (Registry.LocalMachine, @"SOFTWARE\Valve\Steam")
                 })
        {
            using var key = hive.OpenSubKey(keyPath);
            var raw = key?.GetValue("SteamPath") as string ?? key?.GetValue("InstallPath") as string;
            if (!string.IsNullOrWhiteSpace(raw))
            {
                yield return raw.Replace('/', Path.DirectorySeparatorChar);
            }
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            yield return Path.Combine(programFilesX86, "Steam");
        }
    }
}
