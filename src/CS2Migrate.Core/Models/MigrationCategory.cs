namespace CS2Migrate.Core.Models;

[Flags]
public enum MigrationCategory
{
    None = 0,
    Gameplay = 1,
    Keybinds = 2,
    Video = 4,
    Autoexec = 8,
    AllPortable = Gameplay | Keybinds | Video | Autoexec
}
