using System.Windows;

namespace CS2Migrate.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        LanguageService.Initialize();
        base.OnStartup(e);
    }
}
