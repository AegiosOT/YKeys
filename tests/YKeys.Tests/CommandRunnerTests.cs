namespace YKeys.Tests;

[TestClass]
public sealed class CommandRunnerTests
{
    [TestMethod]
    public void Split_PlainCommand()
    {
        Assert.AreEqual(("wt", ""), CommandRunner.Split("wt"));
        Assert.AreEqual(("ytile", "workspace 1"), CommandRunner.Split("ytile workspace 1"));
        Assert.AreEqual(("ytile", "resize left 80"), CommandRunner.Split(" ytile  resize left 80"));
    }

    [TestMethod]
    public void Split_QuotedProgramPath()
    {
        Assert.AreEqual((@"C:\Program Files\App\app.exe", "--flag x"),
            CommandRunner.Split(@"""C:\Program Files\App\app.exe"" --flag x"));
        Assert.AreEqual((@"C:\Program Files\App\app.exe", ""),
            CommandRunner.Split(@"""C:\Program Files\App\app.exe"""));
    }

    [TestMethod]
    public void Resolve_PrefersASiblingExecutableOverPath()
    {
        // ykeys ships beside ytile.exe, so suite bindings must not depend on
        // PATH being current — a stale PATH silently kills every binding at once.
        string sibling = Path.Combine(AppContext.BaseDirectory, "ytile-resolve-probe.exe");
        File.WriteAllText(sibling, string.Empty);
        try
        {
            Assert.AreEqual(sibling, CommandRunner.Resolve("ytile-resolve-probe"));
            Assert.AreEqual(sibling, CommandRunner.Resolve("ytile-resolve-probe.exe"));
        }
        finally
        {
            File.Delete(sibling);
        }
    }

    [TestMethod]
    public void Resolve_LeavesPathsAndUnknownNamesAlone()
    {
        // An explicit path is the user being specific — never second-guess it.
        Assert.AreEqual(@"C:\Windows\notepad.exe", CommandRunner.Resolve(@"C:\Windows\notepad.exe"));
        Assert.AreEqual(@"sub\tool.exe", CommandRunner.Resolve(@"sub\tool.exe"));
        // No sibling by that name: fall through to PATH resolution unchanged.
        Assert.AreEqual("definitely-not-a-sibling", CommandRunner.Resolve("definitely-not-a-sibling"));
        Assert.AreEqual("", CommandRunner.Resolve(""));
    }

    [TestMethod]
    public void Split_UnterminatedQuotePassesThrough()
    {
        (string file, string args) = CommandRunner.Split("\"broken");
        Assert.AreEqual("\"broken", file);
        Assert.AreEqual("", args);
    }
}
