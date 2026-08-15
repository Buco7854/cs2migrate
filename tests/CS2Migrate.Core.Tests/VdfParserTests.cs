using CS2Migrate.Core.Vdf;

namespace CS2Migrate.Core.Tests;

[TestClass]
public sealed class VdfParserTests
{
    [TestMethod]
    public void Parse_ReadsNestedObjectsCommentsAndEscapes()
    {
        const string content = """
            // Steam login metadata
            "users"
            {
                "76561198000000001"
                {
                    "AccountName" "player\"one"
                    "MostRecent" "1"
                }
            }
            """;

        var root = VdfParser.Parse(content);

        Assert.IsTrue(root.TryGetObject("users", out var users));
        Assert.IsTrue(users.TryGetObject("76561198000000001", out var user));
        Assert.IsTrue(user.TryGetString("AccountName", out var accountName));
        Assert.AreEqual("player\"one", accountName);
    }

    [TestMethod]
    public void Parse_RejectsUnclosedObjects()
    {
        Assert.ThrowsExactly<FormatException>(() => VdfParser.Parse("\"users\" { \"id\" \"value\""));
    }
}
