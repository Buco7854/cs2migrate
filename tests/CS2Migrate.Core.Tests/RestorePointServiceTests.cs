using CS2Migrate.Core.Models;

namespace CS2Migrate.Core.Tests;

[TestClass]
public sealed class RestorePointServiceTests
{
    [TestMethod]
    public async Task FindRestorePoints_ListsEveryOperationNewestFirst()
    {
        using var temporary = new TemporaryDirectory();
        var source = CreateAccount(temporary, 101);
        var target = CreateAccount(temporary, 202);
        var backupRoot = temporary.CreateDirectory("backups");
        temporary.WriteFile("source binds", "userdata", "101", "730", "local", "cfg", "cs2_user_keys_0_slot0.vcfg");
        temporary.WriteFile("original binds", "userdata", "202", "730", "local", "cfg", "cs2_user_keys_0_slot0.vcfg");

        var inspector = new FakeProcessInspector();
        await new AccountBackupService(inspector).CreateManualBackupAsync(target, backupRoot);
        await new MigrationEngine(inspector).MigrateAsync(
            new MigrationRequest(source, target, MigrationCategory.Keybinds, backupRoot));

        var points = new RestorePointService(inspector).FindRestorePoints(target, backupRoot);

        Assert.AreEqual(3, points.Count);
        Assert.AreEqual(RestorePointKind.AppliedByMigration, points[0].Kind);
        Assert.AreEqual(RestorePointKind.BeforeMigration, points[1].Kind);
        Assert.AreEqual(RestorePointKind.ManualBackup, points[2].Kind);
        Assert.IsTrue(points[1].CreatedUtc >= points[2].CreatedUtc);
        CollectionAssert.AreEqual(
            new[] { "cs2_user_keys_0_slot0.vcfg" },
            points[1].Files.Select(file => file.Name).ToArray());
    }

    [TestMethod]
    public async Task FindRestorePoints_OffersThePreviousContentsOfEachPoint()
    {
        using var temporary = new TemporaryDirectory();
        var source = CreateAccount(temporary, 101);
        var target = CreateAccount(temporary, 202);
        var backupRoot = temporary.CreateDirectory("backups");
        temporary.WriteFile("source binds", "userdata", "101", "730", "local", "cfg", "cs2_user_keys_0_slot0.vcfg");
        temporary.WriteFile("original binds", "userdata", "202", "730", "local", "cfg", "cs2_user_keys_0_slot0.vcfg");

        var inspector = new FakeProcessInspector();
        await new MigrationEngine(inspector).MigrateAsync(
            new MigrationRequest(source, target, MigrationCategory.Keybinds, backupRoot));

        var points = new RestorePointService(inspector).FindRestorePoints(target, backupRoot);

        Assert.AreEqual(
            "source binds",
            File.ReadAllText(points.Single(p => p.Kind == RestorePointKind.AppliedByMigration).Files.Single().ArchivePath),
            "the version the migration wrote");
        Assert.AreEqual(
            "original binds",
            File.ReadAllText(points.Single(p => p.Kind == RestorePointKind.BeforeMigration).Files.Single().ArchivePath),
            "the version it replaced");
    }

    [TestMethod]
    public async Task RestoreAsync_PutsBackOnlyTheChosenFileAndArchivesWhatItReplaced()
    {
        using var temporary = new TemporaryDirectory();
        var source = CreateAccount(temporary, 101);
        var target = CreateAccount(temporary, 202);
        var backupRoot = temporary.CreateDirectory("backups");
        temporary.WriteFile("source binds", "userdata", "101", "730", "local", "cfg", "cs2_user_keys_0_slot0.vcfg");
        temporary.WriteFile("source sensitivity", "userdata", "101", "730", "local", "cfg", "cs2_user_convars_0_slot0.vcfg");
        temporary.WriteFile("original binds", "userdata", "202", "730", "local", "cfg", "cs2_user_keys_0_slot0.vcfg");
        temporary.WriteFile("original sensitivity", "userdata", "202", "730", "local", "cfg", "cs2_user_convars_0_slot0.vcfg");

        var inspector = new FakeProcessInspector();
        await new MigrationEngine(inspector).MigrateAsync(
            new MigrationRequest(source, target, MigrationCategory.AllPortable, backupRoot));

        var service = new RestorePointService(inspector);
        var point = service.FindRestorePoints(target, backupRoot)
            .Single(candidate => candidate.Kind == RestorePointKind.BeforeMigration);
        var result = await service.RestoreAsync(target, point, ["cs2_user_keys_0_slot0.vcfg"], backupRoot);

        Assert.AreEqual(1, result.FileCount);
        Assert.AreEqual(
            "original binds",
            File.ReadAllText(Path.Combine(target.ConfigDirectory, "cs2_user_keys_0_slot0.vcfg")));
        Assert.AreEqual(
            "source sensitivity",
            File.ReadAllText(Path.Combine(target.ConfigDirectory, "cs2_user_convars_0_slot0.vcfg")),
            "restoring one file must leave the others alone");
        Assert.AreEqual(
            "source binds",
            File.ReadAllText(Path.Combine(result.BackupDirectory, "files", "cs2_user_keys_0_slot0.vcfg")),
            "the replaced contents become their own restore point");
    }

    [TestMethod]
    public async Task RestoreAsync_IsItselfListedAsARestorePoint()
    {
        using var temporary = new TemporaryDirectory();
        var source = CreateAccount(temporary, 101);
        var target = CreateAccount(temporary, 202);
        var backupRoot = temporary.CreateDirectory("backups");
        temporary.WriteFile("source binds", "userdata", "101", "730", "local", "cfg", "cs2_user_keys_0_slot0.vcfg");
        temporary.WriteFile("original binds", "userdata", "202", "730", "local", "cfg", "cs2_user_keys_0_slot0.vcfg");

        var inspector = new FakeProcessInspector();
        await new MigrationEngine(inspector).MigrateAsync(
            new MigrationRequest(source, target, MigrationCategory.Keybinds, backupRoot));

        var service = new RestorePointService(inspector);
        var point = service.FindRestorePoints(target, backupRoot)
            .Single(candidate => candidate.Kind == RestorePointKind.BeforeMigration);
        await service.RestoreAsync(target, point, ["cs2_user_keys_0_slot0.vcfg"], backupRoot);

        var points = service.FindRestorePoints(target, backupRoot);
        Assert.AreEqual(4, points.Count, "the restore adds its own before/after pair");
        Assert.AreEqual(RestorePointKind.AppliedByMigration, points[0].Kind);
        Assert.AreEqual(
            "source binds",
            File.ReadAllText(points[1].Files.Single().ArchivePath),
            "the settings that were live before the restore are recoverable");
    }

    [TestMethod]
    public async Task RestoreAsync_RefusesWhenSteamIsRunning()
    {
        using var temporary = new TemporaryDirectory();
        var source = CreateAccount(temporary, 101);
        var target = CreateAccount(temporary, 202);
        var backupRoot = temporary.CreateDirectory("backups");
        temporary.WriteFile("source binds", "userdata", "101", "730", "local", "cfg", "cs2_user_keys_0_slot0.vcfg");
        temporary.WriteFile("original binds", "userdata", "202", "730", "local", "cfg", "cs2_user_keys_0_slot0.vcfg");

        await new MigrationEngine(new FakeProcessInspector()).MigrateAsync(
            new MigrationRequest(source, target, MigrationCategory.Keybinds, backupRoot));

        var service = new RestorePointService(new FakeProcessInspector("Steam"));
        var point = service.FindRestorePoints(target, backupRoot)
            .Single(candidate => candidate.Kind == RestorePointKind.BeforeMigration);

        var failure = await Assert.ThrowsExactlyAsync<MigrationException>(() =>
            service.RestoreAsync(target, point, ["cs2_user_keys_0_slot0.vcfg"], backupRoot));
        StringAssert.Contains(failure.Message, "Steam");
        Assert.AreEqual(
            "source binds",
            File.ReadAllText(Path.Combine(target.ConfigDirectory, "cs2_user_keys_0_slot0.vcfg")));
    }

    [TestMethod]
    public async Task FindRestorePoints_SkipsFilesTheOperationCreatedRatherThanReplaced()
    {
        using var temporary = new TemporaryDirectory();
        var source = CreateAccount(temporary, 101);
        var target = CreateAccount(temporary, 202);
        var backupRoot = temporary.CreateDirectory("backups");
        temporary.WriteFile("source binds", "userdata", "101", "730", "local", "cfg", "cs2_user_keys_0_slot0.vcfg");

        await new MigrationEngine(new FakeProcessInspector()).MigrateAsync(
            new MigrationRequest(source, target, MigrationCategory.Keybinds, backupRoot));

        var points = new RestorePointService(new FakeProcessInspector()).FindRestorePoints(target, backupRoot);

        Assert.AreEqual(0, points.Count(point => point.Kind == RestorePointKind.BeforeMigration),
            "a file that did not exist before has no earlier version to offer");
        Assert.AreEqual(1, points.Count(point => point.Kind == RestorePointKind.AppliedByMigration),
            "the version the migration wrote is still restorable");
    }

    [TestMethod]
    public void FindRestorePoints_ReturnsNothingWhenNoBackupsExist()
    {
        using var temporary = new TemporaryDirectory();
        var account = CreateAccount(temporary, 202);

        var points = new RestorePointService(new FakeProcessInspector())
            .FindRestorePoints(account, temporary.CreateDirectory("backups"));

        Assert.AreEqual(0, points.Count);
    }

    private static SteamAccount CreateAccount(TemporaryDirectory temporary, uint accountId)
    {
        var userData = temporary.CreateDirectory("userdata", accountId.ToString());
        var config = temporary.CreateDirectory("userdata", accountId.ToString(), "730", "local", "cfg");
        return new SteamAccount(
            accountId,
            SteamConstants.SteamId64Base + accountId,
            $"Player {accountId}",
            $"player{accountId}",
            userData,
            config,
            null,
            false,
            null,
            0);
    }

    private sealed class FakeProcessInspector(params string[] blockers) : IProcessInspector
    {
        public IReadOnlyList<string> GetBlockingProcesses() => blockers;
    }
}
