using System.Windows.Media;
using CS2Migrate.Core.Models;

namespace CS2Migrate.App;

internal sealed class AccountCardViewModel(SteamAccount account)
{
    public SteamAccount Account { get; } = account;
    public string DisplayName => Account.DisplayName;
    public string AccountIdentity => string.IsNullOrWhiteSpace(Account.AccountName)
        ? LanguageService.Format("AccountIdentityFallback", Account.AccountId)
        : $"{Account.AccountName} · {Account.AccountId}";
    public bool IsMostRecent => Account.IsMostRecent;
    public string ConfigSummary => Account.PortableFileCount switch
    {
        0 => LanguageService.Text("NoPortableFiles"),
        1 => LanguageService.Text("OnePortableFile"),
        _ => LanguageService.Format("ManyPortableFiles", Account.PortableFileCount)
    };
    public StatusSeverity HealthSeverity => Account.PortableFileCount > 0
        ? StatusSeverity.Success
        : StatusSeverity.Caution;
    public ImageSource Avatar { get; } = AvatarFactory.Create(account);
    public string LastUsed => Account.LastLogin is null
        ? LanguageService.Text("LastUsedUnknown")
        : LanguageService.Format("LastUsed", Account.LastLogin.Value.LocalDateTime.ToString("g", LanguageService.Culture));
}
