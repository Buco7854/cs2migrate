using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace CS2Migrate.App;

/// <summary>
/// Keeps the window in step with the Windows personalisation settings: the app follows the
/// system light/dark preference and paints with the accent colour the user picked.
/// </summary>
internal static class ThemeService
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static readonly Uri LightTheme = new("Resources/Theme.Light.xaml", UriKind.Relative);
    private static readonly Uri DarkTheme = new("Resources/Theme.Dark.xaml", UriKind.Relative);

    public static void Initialize()
    {
        Apply();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public static void Apply()
    {
        var light = UsesLightTheme();
        SwapThemeDictionary(light ? LightTheme : DarkTheme, light ? DarkTheme : LightTheme);
        ApplyAccent(light);
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color))
        {
            return;
        }

        Application.Current?.Dispatcher.BeginInvoke(Apply);
    }

    private static void SwapThemeDictionary(Uri wanted, Uri unwanted)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var alreadyApplied = dictionaries.Any(dictionary => Matches(dictionary, wanted));
        for (var index = dictionaries.Count - 1; index >= 0; index--)
        {
            if (Matches(dictionaries[index], unwanted))
            {
                dictionaries.RemoveAt(index);
            }
        }

        if (!alreadyApplied)
        {
            dictionaries.Insert(0, new ResourceDictionary { Source = wanted });
        }
    }

    private static bool Matches(ResourceDictionary dictionary, Uri source) =>
        dictionary.Source is not null &&
        dictionary.Source.OriginalString.EndsWith(source.OriginalString, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Fluent uses a darker shade of the accent on light backgrounds and a lighter one on dark
    /// backgrounds so that text keeps its contrast either way.
    /// </summary>
    private static void ApplyAccent(bool light)
    {
        var fill = light ? SystemColors.AccentColorDark1 : SystemColors.AccentColorLight2;
        var text = light ? SystemColors.AccentColorDark2 : SystemColors.AccentColorLight3;
        var resources = Application.Current.Resources;
        resources["AccentFillBrush"] = Frozen(fill);
        resources["AccentFillHoverBrush"] = Frozen(WithAlpha(fill, 0.9));
        resources["AccentFillPressedBrush"] = Frozen(WithAlpha(fill, 0.8));
        resources["AccentTextBrush"] = Frozen(text);
    }

    private static Color WithAlpha(Color color, double alpha) =>
        Color.FromArgb((byte)Math.Round(alpha * 255), color.R, color.G, color.B);

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static bool UsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return true;
        }
    }
}
