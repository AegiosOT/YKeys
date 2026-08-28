namespace YKeys.Tests;

[TestClass]
public sealed class ConfigTests
{
    [TestMethod]
    public void Load_MissingFile_YieldsEmpty()
    {
        YKeysConfig config = YKeysConfig.Load(Path.Combine(Path.GetTempPath(), "ykeys-none", "nope.json"), out string? error);
        Assert.IsNull(error);
        Assert.AreEqual(0, config.Hotkeys.Count);
    }

    [TestMethod]
    public void Load_ParsesHotkeysAndReportsBadOnes()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ykeys-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(path,
            """
            {
              "hotkeys": {
                "alt+1": "ytile workspace 1",
                "alt+enter": "wt",
                "alt+shift+1": "ytile send 1",
                "shift+alt+1": "ytile workspace 9",
                "alt+banana": "ytile retile",
                "alt+shift+x": "   "
              }
            }
            """);
        try
        {
            YKeysConfig config = YKeysConfig.Load(path, out string? error);

            Assert.AreEqual(3, config.Hotkeys.Count);
            Assert.AreEqual("alt+1", config.Hotkeys[0].Chord);
            Assert.AreEqual("ytile workspace 1", config.Hotkeys[0].CommandLine);
            Assert.AreEqual("wt", config.Hotkeys[1].CommandLine);
            // "shift+alt+1" normalizes to the same registration as "alt+shift+1".

            Assert.IsNotNull(error);
            StringAssert.Contains(error, "duplicates");
            StringAssert.Contains(error, "banana");
            StringAssert.Contains(error, "no command");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Load_ExactDuplicateChordKeyIsConfigError()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ykeys-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{ "hotkeys": { "alt+1": "a", "alt+1": "b" } }""");
        try
        {
            // Duplicate JSON keys must not silently last-win: the whole file is
            // rejected (AllowDuplicateProperties = false) and defaults apply.
            YKeysConfig config = YKeysConfig.Load(path, out string? error);
            Assert.IsNotNull(error);
            Assert.AreEqual(0, config.Hotkeys.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Load_BadJson_FallsBackToEmptyWithError()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ykeys-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ not json");
        try
        {
            YKeysConfig config = YKeysConfig.Load(path, out string? error);
            Assert.IsNotNull(error);
            Assert.AreEqual(0, config.Hotkeys.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
