using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace CS2Migrate.App;

/// <summary>
/// Keeps the window in step with the Windows personalisation settings: the app follows the
/// system light/dark preference, paints with the accent colour the user picked, and asks DWM
/// for a matching title bar.
/// </summary>
internal enum ThemePreference
{
    System,
    Light,
    Dark
}

internal static class ThemeService
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    // DWMWA_USE_IMMERSIVE_DARK_MODE. The attribute moved from 19 to 20 in Windows 10 20H1.
    private const int UseImmersiveDarkMode = 20;
    private const int UseImmersiveDarkModeBefore20H1 = 19;

    // Windows 11 22000+ lets the caption be painted outright, which is more dependable than
    // the dark-mode flag alone.
    private const int BorderColorAttribute = 34;
    private const int CaptionColorAttribute = 35;
    private const int CaptionTextColorAttribute = 36;

    private const uint FrameChangedFlags = 0x0001 | 0x0002 | 0x0004 | 0x0020; // NOSIZE|NOMOVE|NOZORDER|FRAMECHANGED

    private static readonly Uri LightTheme = new("Resources/Theme.Light.xaml", UriKind.Relative);
    private static readonly Uri DarkTheme = new("Resources/Theme.Dark.xaml", UriKind.Relative);

    private static readonly string PreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CS2Migrate",
        "theme.txt");

    private static bool _isLight = true;

    /// <summary>Following Windows is the default, but the choice can be overridden and is kept.</summary>
    public static ThemePreference Preference { get; private set; } = ThemePreference.System;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    public static void Initialize()
    {
        Preference = ReadPreference();
        Apply();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    /// <summary>
    /// Without this the app paints itself dark while Windows keeps drawing a light title bar.
    /// </summary>
    public static void RegisterWindow(Window window)
    {
        if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
        {
            ApplyTitleBar(window);
            return;
        }

        window.SourceInitialized += (sender, _) =>
        {
            if (sender is Window initialized)
            {
                ApplyTitleBar(initialized);
            }
        };

        // Some Windows builds ignore the attribute until the frame has been created, so set
        // it again once the window is up.
        window.Loaded += (sender, _) =>
        {
            if (sender is Window loaded)
            {
                ApplyTitleBar(loaded);
            }
        };
    }

    /// <summary>Steps through follow-Windows, light, and dark.</summary>
    public static void Cycle()
    {
        Preference = Preference switch
        {
            ThemePreference.System => ThemePreference.Light,
            ThemePreference.Light => ThemePreference.Dark,
            _ => ThemePreference.System
        };

        WritePreference(Preference);
        Apply();
    }

    public static void Apply()
    {
        _isLight = UsesLightTheme();
        SwapThemeDictionary(_isLight ? LightTheme : DarkTheme, _isLight ? DarkTheme : LightTheme);
        ApplyAccent(_isLight);

        foreach (Window window in Application.Current.Windows)
        {
            ApplyTitleBar(window);
        }
    }

    private static void ApplyTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var useDarkTitleBar = _isLight ? 0 : 1;
        if (DwmSetWindowAttribute(handle, UseImmersiveDarkMode, ref useDarkTitleBar, sizeof(int)) != 0)
        {
            _ = DwmSetWindowAttribute(handle, UseImmersiveDarkModeBefore20H1, ref useDarkTitleBar, sizeof(int));
        }

        // Paint the caption with the same colours as the page. Ignored before Windows 11,
        // where the dark-mode flag above is what takes effect.
        var caption = ColorRef("LayerBackgroundBrush", _isLight ? "#F3F3F3" : "#202020");
        var captionText = ColorRef("TextPrimaryBrush", _isLight ? "#1B1B1B" : "#FFFFFF");
        var border = ColorRef("CardStrokeBrush", _isLight ? "#E5E5E5" : "#1F1F1F");
        _ = DwmSetWindowAttribute(handle, CaptionColorAttribute, ref caption, sizeof(int));
        _ = DwmSetWindowAttribute(handle, CaptionTextColorAttribute, ref captionText, sizeof(int));
        _ = DwmSetWindowAttribute(handle, BorderColorAttribute, ref border, sizeof(int));

        if (window.IsVisible)
        {
            _ = SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, FrameChangedFlags);
        }
    }

    /// <summary>Packs a themed brush into the COLORREF (0x00BBGGRR) that DWM expects.</summary>
    private static int ColorRef(string brushKey, string fallback)
    {
        var color = Application.Current.TryFindResource(brushKey) is SolidColorBrush brush
            ? brush.Color
            : (Color)ColorConverter.ConvertFromString(fallback);
        return color.R | (color.G << 8) | (color.B << 16);
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

    private static ThemePreference ReadPreference()
    {
        try
        {
            return File.Exists(PreferencePath)
                ? File.ReadAllText(PreferencePath).Trim().ToLowerInvariant() switch
                {
                    "light" => ThemePreference.Light,
                    "dark" => ThemePreference.Dark,
                    _ => ThemePreference.System
                }
                : ThemePreference.System;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ThemePreference.System;
        }
    }

    private static void WritePreference(ThemePreference preference)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath)!);
            File.WriteAllText(PreferencePath, preference.ToString().ToLowerInvariant());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The choice still applies for this session.
        }
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static bool UsesLightTheme()
    {
        if (Preference != ThemePreference.System)
        {
            return Preference == ThemePreference.Light;
        }

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
