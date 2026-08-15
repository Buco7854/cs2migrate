using System.Globalization;
using System.IO;
using System.Windows;

namespace CS2Migrate.App;

internal enum AppLanguage
{
    English,
    French
}

internal static class LanguageService
{
    private static readonly string PreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CS2Migrate",
        "language.txt");

    public static AppLanguage Current { get; private set; } = AppLanguage.English;

    public static CultureInfo Culture => Current == AppLanguage.French
        ? CultureInfo.GetCultureInfo("fr-FR")
        : CultureInfo.GetCultureInfo("en-US");

    public static void Initialize()
    {
        var preference = TryReadPreference();
        var detected = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("fr", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.French
            : AppLanguage.English;
        Apply(preference ?? detected, remember: false);
    }

    public static void Toggle() => Apply(
        Current == AppLanguage.English ? AppLanguage.French : AppLanguage.English,
        remember: true);

    public static string Text(string key)
    {
        var value = Application.Current.TryFindResource(key) as string;
        return value ?? key;
    }

    public static string Format(string key, params object?[] arguments) =>
        string.Format(Culture, Text(key), arguments);

    public static string TranslateMigrationError(string message)
    {
        const string closePrefix = "Close ";
        const string closeSuffix = " before migrating settings.";
        if (message.StartsWith(closePrefix, StringComparison.Ordinal) &&
            message.EndsWith(closeSuffix, StringComparison.Ordinal))
        {
            var processNames = message[closePrefix.Length..^closeSuffix.Length]
                .Replace(" and ", Text("ProcessJoiner"), StringComparison.Ordinal);
            return Format("ErrorCloseProcesses", processNames);
        }

        if (message.StartsWith("Integrity verification failed", StringComparison.Ordinal))
        {
            return Text("ErrorIntegrity");
        }

        return message switch
        {
            "Choose two different Steam accounts." => Text("ErrorChooseDifferentAccounts"),
            "Choose at least one supported settings category." => Text("ErrorChooseCategory"),
            "The source account does not have a CS2 config folder yet." => Text("ErrorNoSourceConfig"),
            "No matching CS2 settings were found in the source account." => Text("ErrorNoMatchingFiles"),
            "Choose a source account." => Text("ErrorChooseSource"),
            "Choose a target account." => Text("ErrorChooseTarget"),
            "The target CS2 config path is invalid." => Text("ErrorInvalidTargetPath"),
            "A selected account contains an unexpected CS2 config path." => Text("ErrorUnexpectedPath"),
            "Linked Steam userdata folders are not supported for safety." => Text("ErrorLinkedUserdata"),
            "Linked CS2 config folders are not supported for safety." => Text("ErrorLinkedConfig"),
            "The migration failed. Any changed files were rolled back." => Text("ErrorMigrationFailed"),
            "The backup failed before a verified snapshot could be completed." => Text("ErrorBackupFailed"),
            "The restore failed. Any changed files were rolled back." => Text("ErrorRestoreFailed"),
            "A target setting changed while the migration was being prepared. Refresh and try again." => Text("ErrorTargetChanged"),
            "No portable CS2 settings were found for this account." => Text("ErrorNoPortableFiles"),
            "The temporary session backup is incomplete or damaged." => Text("ErrorDamagedBackup"),
            "The selected backup is not a temporary friend session." => Text("ErrorDamagedBackup"),
            "A backup contains an unsupported file name." => Text("ErrorDamagedBackup"),
            _ => message
        };
    }

    private static void Apply(AppLanguage language, bool remember)
    {
        Current = language;
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.Contains("Resources/Strings.", StringComparison.OrdinalIgnoreCase) == true);
        if (existing is not null)
        {
            dictionaries.Remove(existing);
        }

        var code = language == AppLanguage.French ? "fr" : "en";
        dictionaries.Insert(0, new ResourceDictionary
        {
            Source = new Uri($"Resources/Strings.{code}.xaml", UriKind.Relative)
        });

        CultureInfo.CurrentUICulture = Culture;
        if (remember)
        {
            TryWritePreference(code);
        }
    }

    private static AppLanguage? TryReadPreference()
    {
        try
        {
            if (!File.Exists(PreferencePath))
            {
                return null;
            }

            return File.ReadAllText(PreferencePath).Trim().ToLowerInvariant() switch
            {
                "fr" => AppLanguage.French,
                "en" => AppLanguage.English,
                _ => null
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void TryWritePreference(string code)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath)!);
            File.WriteAllText(PreferencePath, code);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Language switching still works for the current session.
        }
    }
}
