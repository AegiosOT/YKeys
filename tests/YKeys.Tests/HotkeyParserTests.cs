using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace YKeys.Tests;

[TestClass]
public sealed class HotkeyParserTests
{
    private static (HOT_KEY_MODIFIERS Mods, uint Vk) Parse(string chord)
    {
        Assert.IsTrue(HotkeyParser.TryParse(chord, out var mods, out uint vk, out string? error), $"'{chord}' failed: {error}");
        return (mods, vk);
    }

    private static string Reject(string chord)
    {
        Assert.IsFalse(HotkeyParser.TryParse(chord, out _, out _, out string? error), $"'{chord}' unexpectedly parsed");
        Assert.IsNotNull(error);
        return error;
    }

    [TestMethod]
    public void Parse_ModifiersAndKeys()
    {
        var (mods, vk) = Parse("alt+shift+1");
        Assert.AreEqual(HOT_KEY_MODIFIERS.MOD_ALT | HOT_KEY_MODIFIERS.MOD_SHIFT, mods);
        Assert.AreEqual((uint)VIRTUAL_KEY.VK_1, vk);

        (mods, vk) = Parse("ctrl+win+left");
        Assert.AreEqual(HOT_KEY_MODIFIERS.MOD_CONTROL | HOT_KEY_MODIFIERS.MOD_WIN, mods);
        Assert.AreEqual((uint)VIRTUAL_KEY.VK_LEFT, vk);

        (_, vk) = Parse("alt+f11");
        Assert.AreEqual((uint)VIRTUAL_KEY.VK_F11, vk);

        (_, vk) = Parse("alt+enter");
        Assert.AreEqual((uint)VIRTUAL_KEY.VK_RETURN, vk);
    }

    [TestMethod]
    public void Parse_ToleratesCaseAndWhitespace()
    {
        var (mods, vk) = Parse("Alt + Shift + Q");
        Assert.AreEqual(HOT_KEY_MODIFIERS.MOD_ALT | HOT_KEY_MODIFIERS.MOD_SHIFT, mods);
        Assert.AreEqual((uint)VIRTUAL_KEY.VK_Q, vk);

        (mods, _) = Parse("CONTROL+space");
        Assert.AreEqual(HOT_KEY_MODIFIERS.MOD_CONTROL, mods);
    }

    [TestMethod]
    public void Parse_BareMacroKeysOnly()
    {
        // F13-F24 exist only as deliberate macro keys — bare binding allowed.
        var (mods, vk) = Parse("f13");
        Assert.AreEqual((HOT_KEY_MODIFIERS)0, mods);
        Assert.AreEqual((uint)VIRTUAL_KEY.VK_F13, vk);

        StringAssert.Contains(Reject("x"), "modifier");
        StringAssert.Contains(Reject("f5"), "modifier");
    }

    [TestMethod]
    public void Parse_AcceptsOemSpellingsFromWhkdConfigs()
    {
        // YTile ships a whkdrc using oem_plus/oem_minus; both formats must agree
        // or translating between them silently drops bindings.
        Assert.AreEqual(Parse("alt+plus").Vk, Parse("alt+oem_plus").Vk);
        Assert.AreEqual(Parse("alt+minus").Vk, Parse("alt+oem_minus").Vk);
        Assert.AreEqual(Parse("alt+semicolon").Vk, Parse("alt+oem_1").Vk);
        Assert.AreEqual(Parse("alt+lbracket").Vk, Parse("alt+oem_4").Vk);
        Assert.AreEqual(Parse("alt+quote").Vk, Parse("alt+oem_7").Vk);
        Assert.AreEqual((uint)VIRTUAL_KEY.VK_OEM_PLUS, Parse("alt+shift+oem_plus").Vk);
    }

    [TestMethod]
    public void Parse_NumpadKeysAreDistinctFromTheNumberRow()
    {
        Assert.AreEqual((uint)VIRTUAL_KEY.VK_NUMPAD1, Parse("alt+numpad1").Vk);
        Assert.AreNotEqual(Parse("alt+1").Vk, Parse("alt+numpad1").Vk);
        Assert.AreEqual((uint)VIRTUAL_KEY.VK_ADD, Parse("alt+numpad_add").Vk);
        Assert.AreEqual((uint)VIRTUAL_KEY.VK_DIVIDE, Parse("alt+numpad_divide").Vk);
        Assert.AreEqual((uint)VIRTUAL_KEY.VK_DECIMAL, Parse("alt+numpad_dot").Vk);
    }

    [TestMethod]
    public void Parse_RejectsBadChords()
    {
        StringAssert.Contains(Reject("alt+banana"), "unknown key");
        StringAssert.Contains(Reject("hyper+x"), "unknown modifier");
        StringAssert.Contains(Reject("alt+alt+x"), "repeated");
        StringAssert.Contains(Reject("alt+f12"), "f12");
        StringAssert.Contains(Reject("alt++1"), "empty part");
        StringAssert.Contains(Reject(""), "empty hotkey");
    }
}
