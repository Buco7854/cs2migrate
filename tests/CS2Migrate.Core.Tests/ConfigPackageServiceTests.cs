using CS2Migrate.Core.Models;

namespace CS2Migrate.Core.Tests;

[TestClass]
public sealed class ConfigPackageServiceTests
{
    [TestMethod]
    public async Task ExportThenImport_MovesSettingsBetweenAccountsThroughAFile()
    {
        using var temporary = new TemporaryDirectory();
        var exported = CreateAccount(temporary, 101);
        var imported = CreateAccount(temporary, 202);
        var backupRoot = temporary.CreateDirectory("backups");
        temporary.WriteFile("my binds", "userdata", "101", "730", "local", "cfg", "cs2_user_keys_0_slot0.vcfg");
        temporary.WriteFile("my sensitivity", "userdata", "101", "730", "local", "cfg", "cs2_user_convars_0_slot0.vcfg");
        temporary.WriteFile("their binds", "userdata", "202", "730", "local", "cfg", "cs2_user_keys_0_slot0.vcfg");

        var service = new ConfigPackageService(new FakeProcessInspector());
        var file = Path.Combine(temporary.Path, "setup" + ConfigPackageService.FileExtension);
        var written = await service.ExportAsync(exported, file);

        Assert.IsTrue(File.Exists(file));
        Assert.AreEqual(2, written.Entries.Count);

        var package = service.Read(file);
        Assert.AreEqual("Player 101", package.AccountName);
        CollectionAssert.AreEquivalent(
            new[] { "cs2_user_keys_0_slot0.vcfg", "cs2_user_convars_0_slot0.vcfg" },
            package.Entries.Select(entry => entry.Name).ToArray());

        var result = await service.ImportAsync(
            imported,
            package,
            package.Entries.Select(entry => entry.Name).ToArray(),
            backupRoot);

        Assert.AreEqual(2, result.FileCount);
        Assert.AreEqual("my binds", File.ReadAllText(Path.Combine(imported.ConfigDirectory, "cs2_user_keys_0_slot0.vcfg")));
        Assert.AreEqual("my sensitivity", File.ReadAllText(Path.Combine(imported.ConfigDirectory, "cs2_user_convars_0_slot0.vcfg")));
    }

    [TestMethod]
    public async Task ImportAsync_WritesOnlyTheChosenFilesAndKeepsTheOldOnesInHistory()
    {
        using var temporary = new TemporaryDirectory();
        var exported = CreateAccount(temporary, 101);
        var imported = CreateAccount(temporary, 202);
        var backupRoot = temporary.CreateDirectory("backups");
        temporary.WriteFile("my binds", "userdata", "101", "730", "local", "cfg", "cs2_user_keys_0_slot0.vcfg");
        temporary.WriteFile("my sensitivity", "userdata", "101", "730", "local", "cfg", "cs2_user_convars_0_slot0.vcfg");
        temporary.WriteFile("their binds", "userdata", "202", "730", "local", "cfg", "cs2_user_keys_0_slot0.vcfg");
        temporary.WriteFile("their sensitivity", "userdata", "202", "730", "local", "cfg", "cs2_user_convars_0_slot0.vcfg");

        var inspector = new FakeProcessInspector();
        var service = new ConfigPackageService(inspector);
        var file = Path.Combine(temporary.Path, "setup" + ConfigPackageService.FileExtension);
        await service.ExportAsync(exported, file);
        var package = service.Read(file);

        await service.ImportAsync(imported, package, ["cs2_user_keys_0_slot0.vcfg"], backupRoot);

        Assert.AreEqual("my binds", File.ReadAllText(Path.Combine(imported.ConfigDirectory, "cs2_user_keys_0_slot0.vcfg")));
        Assert.AreEqual(
            "their sensitivity",
            File.ReadAllText(Path.Combine(imported.ConfigDirectory, "cs2_user_convars_0_slot0.vcfg")),
            "an import must not touch files that were not chosen");

        var replaced = new RestorePointService(inspector)
            .FindRestorePoints(imported, backupRoot)
            .Single(point => point.Kind == RestorePointKind.BeforeMigration);
        Assert.AreEqual("their binds", File.ReadAllText(replaced.Files.Single().ArchivePath));
    }

    [TestMethod]
    public async Task ImportAsync_RefusesWhileSteamIsRunning()
    {
        using var temporary = new TemporaryDirectory();
        var exported = CreateAccount(temporary, 101);
        var imported = CreateAccount(temporary, 202);
        var backupRoot = temporary.CreateDirectory("backups");
        temporary.WriteFile("my binds", "userdata", "101", "730", "local", "cfg", "cs2_user_keys_0_slot0.vcfg");
        temporary.WriteFile("their binds", "userdata", "202", "730", "local", "cfg", "cs2_user_keys_0_slot0.vcfg");

        var file = Path.Combine(temporary.Path, "setup" + ConfigPackageService.FileExtension);
        await new ConfigPackageService(new FakeProcessInspector()).ExportAsync(exported, file);

        var blocked = new ConfigPackageService(new FakeProcessInspector("Steam"));
        var package = blocked.Read(file);

        await Assert.ThrowsExactlyAsync<MigrationException>(() =>
            blocked.ImportAsync(imported, package, ["cs2_user_keys_0_slot0.vcfg"], backupRoot));
        Assert.AreEqual("their binds", File.ReadAllText(Path.Combine(imported.ConfigDirectory, "cs2_user_keys_0_slot0.vcfg")));
    }

    [TestMethod]
    public void Read_RejectsAFileThatIsNotAPackage()
    {
        using var temporary = new TemporaryDirectory();
        var file = temporary.WriteFile("not a zip", "random.cs2config");

        Assert.ThrowsExactly<MigrationException>(() => new ConfigPackageService(new FakeProcessInspector()).Read(file));
    }

    [TestMethod]
    public async Task Read_RejectsAPackageWhoseContentsWereTamperedWith()
    {
        using var temporary = new TemporaryDirectory();
        var exported = CreateAccount(temporary, 101);
        temporary.WriteFile("my binds", "userdata", "101", "730", "local", "cfg", "cs2_user_keys_0_slot0.vcfg");

        var service = new ConfigPackageService(new FakeProcessInspector());
        var file = Path.Combine(temporary.Path, "setup" + ConfigPackageService.FileExtension);
        await service.ExportAsync(exported, file);

        using (var archive = System.IO.Compression.ZipFile.Open(file, System.IO.Compression.ZipArchiveMode.Update))
        {
            archive.GetEntry("files/cs2_user_keys_0_slot0.vcfg")!.Delete();
        }

        Assert.ThrowsExactly<MigrationException>(() => service.Read(file));
    }

    [TestMethod]
    public async Task ExportAsync_RefusesAnAccountWithNothingToExport()
    {
        using var temporary = new TemporaryDirectory();
        var empty = CreateAccount(temporary, 101);

        await Assert.ThrowsExactlyAsync<MigrationException>(() =>
            new ConfigPackageService(new FakeProcessInspector()).ExportAsync(
                empty,
                Path.Combine(temporary.Path, "empty" + ConfigPackageService.FileExtension)));
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
