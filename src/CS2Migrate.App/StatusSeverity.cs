namespace CS2Migrate.App;

/// <summary>
/// Mirrors the severities a Windows 11 info bar can show. The view maps each value to the
/// matching Fluent background, stroke, and glyph so no colour lives in the view model.
/// </summary>
public enum StatusSeverity
{
    Informational,
    Success,
    Caution,
    Critical
}
