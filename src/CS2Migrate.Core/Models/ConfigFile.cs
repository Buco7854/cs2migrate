namespace CS2Migrate.Core.Models;

public sealed record ConfigFile(
    string Name,
    string FullPath,
    MigrationCategory Category,
    long Length,
    DateTimeOffset LastWriteTime);
