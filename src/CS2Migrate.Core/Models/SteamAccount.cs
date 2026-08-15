namespace CS2Migrate.Core.Models;

public sealed record SteamAccount(
    uint AccountId,
    ulong SteamId64,
    string PersonaName,
    string AccountName,
    string UserDataDirectory,
    string ConfigDirectory,
    string? AvatarPath,
    bool IsMostRecent,
    DateTimeOffset? LastLogin,
    int PortableFileCount)
{
    public string DisplayName => string.IsNullOrWhiteSpace(PersonaName)
        ? (string.IsNullOrWhiteSpace(AccountName) ? $"Steam account {AccountId}" : AccountName)
        : PersonaName;
}
