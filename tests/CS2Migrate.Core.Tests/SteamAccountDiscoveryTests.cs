namespace CS2Migrate.Core.Tests;

[TestClass]
public sealed class SteamAccountDiscoveryTests
{
    [TestMethod]
    public void Discover_MatchesLoginMetadataAndCachedAvatar()
    {
        using var temporary = new TemporaryDirectory();
        const uint accountId = 39734273;
        var steamId64 = SteamConstants.SteamId64Base + accountId;
        temporary.WriteFile($$"""
            "users"
            {
                "{{steamId64}}"
                {
                    "AccountName" "sprinter"
                    "PersonaName" "Fast Entry"
                    "MostRecent" "1"
                    "Timestamp" "1735689600"
                }
            }
            """, "config", "loginusers.vdf");
        var avatar = temporary.WriteFile("not-a-real-image", "config", "avatarcache", $"{steamId64}.png");
        temporary.WriteFile("settings", "userdata", accountId.ToString(), "730", "local", "cfg", "cs2_user_convars_0_slot0.vcfg");
        temporary.CreateDirectory("userdata", "0");
        temporary.CreateDirectory("userdata", "not-an-account");

        var accounts = new SteamAccountDiscovery().Discover(temporary.Path);

        Assert.HasCount(1, accounts);
        Assert.AreEqual(accountId, accounts[0].AccountId);
        Assert.AreEqual("Fast Entry", accounts[0].DisplayName);
        Assert.AreEqual("sprinter", accounts[0].AccountName);
        Assert.IsTrue(accounts[0].IsMostRecent);
        Assert.AreEqual(avatar, accounts[0].AvatarPath);
        Assert.AreEqual(1, accounts[0].PortableFileCount);
    }
}
