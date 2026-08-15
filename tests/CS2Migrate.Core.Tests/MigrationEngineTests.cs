using System.Text.Json;
using CS2Migrate.Core.Models;

namespace CS2Migrate.Core.Tests;

[TestClass]
public sealed class MigrationEngineTests
{
    [TestMethod]
    public async Task MigrateAsync_BackupsReplacesAndPreservesUnrelatedFiles()
    {
        using var temporary = new TemporaryDirectory();
        var source = CreateAccount(temporary, 101);
        var target = CreateAccount(temporary, 202);
        temporary.WriteFile("new sensitivity", "userdata", "101", "730", "local", "cfg", "cs2_user_convars_0_slot0.vcfg");
        temporary.WriteFile("new binds", "userdata", "101", "730", "local", "cfg", "cs2_user_keys_0_slot0.vcfg");
        temporary.WriteFile("old sensitivity", "userdata", "202", "730", "local", "cfg", "cs2_user_convars_0_slot0.vcfg");
        temporary.WriteFile("cloud marker", "userdata", "202", "730", "local", "cfg", "steam_autocloud.vdf");
        temporary.WriteFile("machine", "userdata", "202", "730", "local", "cfg", "cs2_machine_convars.vcfg");
        var backupRoot = temporary.CreateDirectory("backups");
        var engine = new MigrationEngine(new FakeProcessInspector());

        var result = await engine.MigrateAsync(new MigrationRequest(
            source,
            target,
            MigrationCategory.Gameplay | MigrationCategory.Keybinds,
            backupRoot));

        Assert.AreEqual("new sensitivity", File.ReadAllText(Path.Combine(target.ConfigDirectory, "cs2_user_convars_0_slot0.vcfg")));
        Assert.AreEqual("new binds", File.ReadAllText(Path.Combine(target.ConfigDirectory, "cs2_user_keys_0_slot0.vcfg")));
        Assert.AreEqual("cloud marker", File.ReadAllText(Path.Combine(target.ConfigDirectory, "steam_autocloud.vdf")));
        Assert.AreEqual("machine", File.ReadAllText(Path.Combine(target.ConfigDirectory, "cs2_machine_convars.vcfg")));
        Assert.AreEqual("old sensitivity", File.ReadAllText(Path.Combine(result.BackupDirectory, "files", "cs2_user_convars_0_slot0.vcfg")));
        Assert.IsTrue(File.Exists(Path.Combine(result.BackupDirectory, "manifest.json")));
        Assert.IsTrue(File.Exists(Path.Combine(result.BackupDirectory, "completed.txt")));
        Assert.AreEqual(
            "new sensitivity",
            File.ReadAllText(Path.Combine(
                result.BackupDirectory,
                "snapshot-userdata",
                "730",
                "local",
                "cfg",
                "cs2_user_convars_0_slot0.vcfg")));
        Assert.AreEqual(2, result.FileCount);

        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.BackupDirectory, "manifest.json")));
        Assert.AreEqual(1, manifest.RootElement.GetProperty("SchemaVersion").GetInt32());
        Assert.HasCount(2, manifest.RootElement.GetProperty("Files").EnumerateArray().ToArray());
    }

    [TestMethod]
    public async Task CloudRecovery_ReappliesSealedFilesAfterRemoteReplacement()
    {
        using var temporary = new TemporaryDirectory();
        var source = CreateAccount(temporary, 101);
        var target = CreateAccount(temporary, 202);
        temporary.WriteFile("migrated", "userdata", "101", "730", "local", "cfg", "config.cfg");
        temporary.WriteFile("original", "userdata", "202", "730", "local", "cfg", "config.cfg");
        var backupRoot = temporary.CreateDirectory("backups");
        var engine = new MigrationEngine(new FakeProcessInspector());
        await engine.MigrateAsync(new MigrationRequest(source, target, MigrationCategory.Gameplay, backupRoot));

        File.WriteAllText(Path.Combine(target.ConfigDirectory, "config.cfg"), "restored from cloud");
        var recovery = new CloudRecoveryService().FindLatestMismatch([target], backupRoot);

        Assert.IsNotNull(recovery);
        Assert.AreEqual(1, recovery.ChangedFileCount);

        await engine.MigrateAsync(new MigrationRequest(
            recovery.SnapshotSource,
            recovery.Target,
            MigrationCategory.AllPortable,
            backupRoot));

        Assert.AreEqual("migrated", File.ReadAllText(Path.Combine(target.ConfigDirectory, "config.cfg")));
        Assert.IsNull(new CloudRecoveryService().FindLatestMismatch([target], backupRoot));
    }

    [TestMethod]
    public async Task CloudRecovery_ChecksEveryDiscoveredTargetAccount()
    {
        using var temporary = new TemporaryDirectory();
        var source = CreateAccount(temporary, 101);
        var firstTarget = CreateAccount(temporary, 202);
        var secondTarget = CreateAccount(temporary, 303);
        temporary.WriteFile("migrated", "userdata", "101", "730", "local", "cfg", "config.cfg");
        temporary.WriteFile("first", "userdata", "202", "730", "local", "cfg", "config.cfg");
        temporary.WriteFile("second", "userdata", "303", "730", "local", "cfg", "config.cfg");
        var backupRoot = temporary.CreateDirectory("backups");
        var engine = new MigrationEngine(new FakeProcessInspector());
        await engine.MigrateAsync(new MigrationRequest(source, firstTarget, MigrationCategory.Gameplay, backupRoot));
        await engine.MigrateAsync(new MigrationRequest(source, secondTarget, MigrationCategory.Gameplay, backupRoot));
        File.WriteAllText(Path.Combine(secondTarget.ConfigDirectory, "config.cfg"), "restored from cloud");

        var recovery = new CloudRecoveryService().FindLatestMismatch([firstTarget, secondTarget], backupRoot);

        Assert.IsNotNull(recovery);
        Assert.AreEqual(secondTarget.AccountId, recovery.Target.AccountId);
    }

    [TestMethod]
    public async Task ManualBackup_RestoresLatestAccountConfiguration()
    {
        using var temporary = new TemporaryDirectory();
        var account = CreateAccount(temporary, 202);
        temporary.WriteFile("protected", "userdata", "202", "730", "local", "cfg", "config.cfg");
        var backupRoot = temporary.CreateDirectory("backups");
        var service = new AccountBackupService(new FakeProcessInspector());
        var backup = await service.CreateManualBackupAsync(account, backupRoot);
        File.WriteAllText(Path.Combine(account.ConfigDirectory, "config.cfg"), "changed");

        var latest = service.FindLatestManualBackup(account, backupRoot);

        Assert.IsNotNull(latest);
        Assert.AreEqual(backup.ArchiveDirectory, latest.ArchiveDirectory);
        await service.RestoreManualBackupAsync(latest, backupRoot);
        Assert.AreEqual("protected", File.ReadAllText(Path.Combine(account.ConfigDirectory, "config.cfg")));
    }

    [TestMethod]
    public async Task TemporaryFriendSession_RestoresOriginalsAndRemovesIntroducedFiles()
    {
        using var temporary = new TemporaryDirectory();
        var source = CreateAccount(temporary, 101);
        var friend = CreateAccount(temporary, 202);
        temporary.WriteFile("my settings", "userdata", "101", "730", "local", "cfg", "config.cfg");
        temporary.WriteFile("my keys", "userdata", "101", "730", "local", "cfg", "cs2_user_keys_0_slot0.vcfg");
        temporary.WriteFile("friend settings", "userdata", "202", "730", "local", "cfg", "config.cfg");
        var backupRoot = temporary.CreateDirectory("backups");
        var inspector = new FakeProcessInspector();
        var engine = new MigrationEngine(inspector);
        var service = new AccountBackupService(inspector);
        await engine.MigrateAsync(new MigrationRequest(
            source,
            friend,
            MigrationCategory.AllPortable,
            backupRoot,
            MigrationPurpose.TemporaryFriendSession));

        var pending = service.FindPendingTemporarySession([friend], backupRoot);

        Assert.IsNotNull(pending);
        Assert.AreEqual(2, pending.ChangedFileCount);
        await service.RestoreTemporarySessionAsync(pending, backupRoot);
        Assert.AreEqual("friend settings", File.ReadAllText(Path.Combine(friend.ConfigDirectory, "config.cfg")));
        Assert.IsFalse(File.Exists(Path.Combine(friend.ConfigDirectory, "cs2_user_keys_0_slot0.vcfg")));
        Assert.IsNull(service.FindPendingTemporarySession([friend], backupRoot));
        Assert.IsNull(new CloudRecoveryService().FindLatestMismatch([friend], backupRoot));
    }

    [TestMethod]
    public async Task TemporaryFriendSession_RestoresEvenWhenCurrentSettingsAreMissing()
    {
        using var temporary = new TemporaryDirectory();
        var source = CreateAccount(temporary, 101);
        var friend = CreateAccount(temporary, 202);
        temporary.WriteFile("my settings", "userdata", "101", "730", "local", "cfg", "config.cfg");
        temporary.WriteFile("friend settings", "userdata", "202", "730", "local", "cfg", "config.cfg");
        var backupRoot = temporary.CreateDirectory("backups");
        var inspector = new FakeProcessInspector();
        var engine = new MigrationEngine(inspector);
        var service = new AccountBackupService(inspector);
        await engine.MigrateAsync(new MigrationRequest(
            source,
            friend,
            MigrationCategory.AllPortable,
            backupRoot,
            MigrationPurpose.TemporaryFriendSession));
        File.Delete(Path.Combine(friend.ConfigDirectory, "config.cfg"));

        var pending = service.FindPendingTemporarySession([friend], backupRoot);

        Assert.IsNotNull(pending);
        await service.RestoreTemporarySessionAsync(pending, backupRoot);
        Assert.AreEqual("friend settings", File.ReadAllText(Path.Combine(friend.ConfigDirectory, "config.cfg")));
    }

    [TestMethod]
    public async Task MigrateAsync_StopsBeforeWritingWhenSteamIsRunning()
    {
        using var temporary = new TemporaryDirectory();
        var source = CreateAccount(temporary, 101);
        var target = CreateAccount(temporary, 202);
        temporary.WriteFile("source", "userdata", "101", "730", "local", "cfg", "config.cfg");
        temporary.WriteFile("target", "userdata", "202", "730", "local", "cfg", "config.cfg");
        var engine = new MigrationEngine(new FakeProcessInspector("Steam"));

        var exception = await Assert.ThrowsExactlyAsync<MigrationException>(() => engine.MigrateAsync(new MigrationRequest(
            source,
            target,
            MigrationCategory.Gameplay,
            temporary.CreateDirectory("backups"))));

        StringAssert.Contains(exception.Message, "Close Steam");
        Assert.AreEqual("target", File.ReadAllText(Path.Combine(target.ConfigDirectory, "config.cfg")));
    }

    [TestMethod]
    public async Task MigrateAsync_StopsIfTargetChangesDuringPreparation()
    {
        using var temporary = new TemporaryDirectory();
        var source = CreateAccount(temporary, 101);
        var target = CreateAccount(temporary, 202);
        temporary.WriteFile("source", "userdata", "101", "730", "local", "cfg", "config.cfg");
        temporary.WriteFile("target", "userdata", "202", "730", "local", "cfg", "config.cfg");
        var targetPath = Path.Combine(target.ConfigDirectory, "config.cfg");
        var inspector = new CallbackProcessInspector(
            2,
            () => File.WriteAllText(targetPath, "concurrent change"));
        var engine = new MigrationEngine(inspector);

        var exception = await Assert.ThrowsExactlyAsync<MigrationException>(() => engine.MigrateAsync(new MigrationRequest(
            source,
            target,
            MigrationCategory.Gameplay,
            temporary.CreateDirectory("backups"))));

        StringAssert.Contains(exception.Message, "changed while the migration was being prepared");
        Assert.AreEqual("concurrent change", File.ReadAllText(targetPath));
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

    private sealed class CallbackProcessInspector(int callNumber, Action callback) : IProcessInspector
    {
        private int _calls;

        public IReadOnlyList<string> GetBlockingProcesses()
        {
            _calls++;
            if (_calls == callNumber)
            {
                callback();
            }

            return [];
        }
    }
}
