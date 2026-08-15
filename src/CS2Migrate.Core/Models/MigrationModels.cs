namespace CS2Migrate.Core.Models;

public sealed record MigrationRequest(
    SteamAccount Source,
    SteamAccount Target,
    MigrationCategory Categories,
    string BackupRoot,
    MigrationPurpose Purpose = MigrationPurpose.Standard);

public enum MigrationPurpose
{
    Standard,
    TemporaryFriendSession,
    ManualBackup,
    SafetyRollback
}

public sealed record MigrationPreviewFile(
    string Name,
    MigrationCategory Category,
    long Length,
    bool ReplacesExisting);

public sealed record MigrationPreview(
    IReadOnlyList<MigrationPreviewFile> Files,
    long TotalBytes)
{
    public int ReplacedCount => Files.Count(file => file.ReplacesExisting);
    public int NewCount => Files.Count - ReplacedCount;
}

public sealed record MigrationProgress(string Stage, string Detail, int Completed, int Total);

public sealed record MigrationResult(
    string BackupDirectory,
    int FileCount,
    long TotalBytes,
    IReadOnlyList<string> FileNames);

public sealed record CloudRecoveryCandidate(
    SteamAccount Target,
    SteamAccount SnapshotSource,
    string ArchiveDirectory,
    int ChangedFileCount,
    int ExpectedFileCount);

public sealed record AccountBackup(
    SteamAccount Account,
    SteamAccount SnapshotSource,
    string ArchiveDirectory,
    DateTimeOffset CreatedUtc,
    int FileCount);

public sealed record TemporarySessionRecovery(
    SteamAccount Target,
    string ArchiveDirectory,
    DateTimeOffset CreatedUtc,
    int ChangedFileCount,
    int FileCount,
    IReadOnlyList<string> FileNames);

internal sealed record BackupManifest(
    int SchemaVersion,
    DateTimeOffset CreatedUtc,
    string SourceAccountId,
    string SourceSteamId64,
    string TargetAccountId,
    string TargetSteamId64,
    string Categories,
    string Purpose,
    IReadOnlyList<BackupManifestFile> Files);

internal sealed record BackupManifestFile(
    string Name,
    string Category,
    string SourceSha256,
    bool TargetExisted,
    string? TargetSha256);
