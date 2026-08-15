using System.Globalization;
using CS2Migrate.Core.Models;
using CS2Migrate.Core.Vdf;

namespace CS2Migrate.Core;

public sealed class SteamAccountDiscovery
{
    public IReadOnlyList<SteamAccount> Discover(string steamDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(steamDirectory);
        var root = Path.GetFullPath(steamDirectory);
        var userData = Path.Combine(root, "userdata");
        if (!Directory.Exists(userData))
        {
            throw new DirectoryNotFoundException($"Steam userdata was not found at '{userData}'.");
        }

        var loginUsers = ReadLoginUsers(Path.Combine(root, "config", "loginusers.vdf"));
        var avatarCache = Path.Combine(root, "config", "avatarcache");
        var accounts = new List<SteamAccount>();

        foreach (var directory in new DirectoryInfo(userData).EnumerateDirectories())
        {
            if (!uint.TryParse(directory.Name, NumberStyles.None, CultureInfo.InvariantCulture, out var accountId) || accountId == 0)
            {
                continue;
            }

            if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                continue;
            }

            var steamId64 = SteamConstants.SteamId64Base + accountId;
            loginUsers.TryGetValue(steamId64.ToString(CultureInfo.InvariantCulture), out var login);
            var configDirectory = Path.Combine(directory.FullName, SteamConstants.Cs2AppId, "local", "cfg");
            var files = ConfigCatalog.FindFiles(configDirectory);

            accounts.Add(new SteamAccount(
                accountId,
                steamId64,
                login?.PersonaName ?? string.Empty,
                login?.AccountName ?? string.Empty,
                directory.FullName,
                configDirectory,
                FindAvatar(avatarCache, steamId64),
                login?.IsMostRecent ?? false,
                login?.LastLogin,
                files.Count));
        }

        return accounts
            .OrderByDescending(account => account.IsMostRecent)
            .ThenByDescending(account => account.LastLogin)
            .ThenBy(account => account.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static Dictionary<string, LoginUser> ReadLoginUsers(string path)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, LoginUser>(StringComparer.Ordinal);
        }

        try
        {
            var root = VdfParser.Parse(File.ReadAllText(path));
            if (!root.TryGetObject("users", out var users))
            {
                return new Dictionary<string, LoginUser>(StringComparer.Ordinal);
            }

            var result = new Dictionary<string, LoginUser>(StringComparer.Ordinal);
            foreach (var entry in users.Objects())
            {
                entry.Value.TryGetString("PersonaName", out var personaName);
                entry.Value.TryGetString("AccountName", out var accountName);
                entry.Value.TryGetString("MostRecent", out var mostRecent);
                entry.Value.TryGetString("Timestamp", out var timestampValue);

                DateTimeOffset? lastLogin = null;
                if (long.TryParse(timestampValue, NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp))
                {
                    try
                    {
                        lastLogin = DateTimeOffset.FromUnixTimeSeconds(timestamp);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        lastLogin = null;
                    }
                }

                result[entry.Key] = new LoginUser(
                    personaName,
                    accountName,
                    mostRecent == "1",
                    lastLogin);
            }

            return result;
        }
        catch (IOException)
        {
            return new Dictionary<string, LoginUser>(StringComparer.Ordinal);
        }
        catch (UnauthorizedAccessException)
        {
            return new Dictionary<string, LoginUser>(StringComparer.Ordinal);
        }
        catch (FormatException)
        {
            return new Dictionary<string, LoginUser>(StringComparer.Ordinal);
        }
    }

    private static string? FindAvatar(string avatarCache, ulong steamId64)
    {
        if (!Directory.Exists(avatarCache))
        {
            return null;
        }

        foreach (var extension in new[] { ".png", ".jpg", ".jpeg" })
        {
            var candidate = Path.Combine(avatarCache, $"{steamId64}{extension}");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private sealed record LoginUser(
        string PersonaName,
        string AccountName,
        bool IsMostRecent,
        DateTimeOffset? LastLogin);
}
