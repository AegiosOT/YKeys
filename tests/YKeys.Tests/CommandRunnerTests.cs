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
    public void Split_UnterminatedQuotePassesThrough()
    {
        (string file, string args) = CommandRunner.Split("\"broken");
        Assert.AreEqual("\"broken", file);
        Assert.AreEqual("", args);
    }
}
