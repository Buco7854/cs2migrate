using CS2Migrate.Core.Models;

namespace CS2Migrate.Core.Tests;

[TestClass]
public sealed class ConfigCatalogTests
{
    [TestMethod]
    [DataRow("cs2_user_convars_0_slot0.vcfg", MigrationCategory.Gameplay)]
    [DataRow("config.cfg", MigrationCategory.Gameplay)]
    [DataRow("cs2_user_keys_0_slot0.vcfg", MigrationCategory.Keybinds)]
    [DataRow("cs2_video.txt", MigrationCategory.Video)]
    [DataRow("autoexec.cfg", MigrationCategory.Autoexec)]
    [DataRow("steam_autocloud.vdf", MigrationCategory.None)]
    [DataRow("cs2_machine_convars.vcfg", MigrationCategory.None)]
    [DataRow("remotecache.vdf", MigrationCategory.None)]
    public void Classify_ReturnsExpectedCategory(string fileName, MigrationCategory expected)
    {
        Assert.AreEqual(expected, ConfigCatalog.Classify(fileName));
    }

    [TestMethod]
    public void FindFiles_FiltersByCategoryAndIgnoresSubdirectories()
    {
        using var temporary = new TemporaryDirectory();
        var config = temporary.CreateDirectory("cfg");
        temporary.WriteFile("gameplay", "cfg", "cs2_user_convars_0_slot0.vcfg");
        temporary.WriteFile("keys", "cfg", "cs2_user_keys_0_slot0.vcfg");
        temporary.WriteFile("nested", "cfg", "nested", "autoexec.cfg");

        var files = ConfigCatalog.FindFiles(config, MigrationCategory.Keybinds);

        Assert.HasCount(1, files);
        Assert.AreEqual("cs2_user_keys_0_slot0.vcfg", files[0].Name);
    }
}
