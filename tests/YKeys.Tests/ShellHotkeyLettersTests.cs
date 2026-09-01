namespace YKeys.Tests;

[TestClass]
public sealed class ShellHotkeyLettersTests
{
    [TestMethod]
    public void Normalize_UppercasesSortsAndDeduplicates()
    {
        Assert.IsTrue(ShellHotkeyLetters.TryNormalize("qre", out string l, out _));
        Assert.AreEqual("EQR", l);

        Assert.IsTrue(ShellHotkeyLetters.TryNormalize("Q,R,E", out l, out _));
        Assert.AreEqual("EQR", l);

        Assert.IsTrue(ShellHotkeyLetters.TryNormalize("Q R E", out l, out _));
        Assert.AreEqual("EQR", l);

        Assert.IsTrue(ShellHotkeyLetters.TryNormalize("QQqQ", out l, out _));
        Assert.AreEqual("Q", l);
    }

    [TestMethod]
    public void Normalize_RejectsNonLettersByName()
    {
        // Digits and OEM keys genuinely cannot be suppressed this way, so a
        // silent drop would be a lie about what the command did.
        Assert.IsFalse(ShellHotkeyLetters.TryNormalize("Q1", out _, out string? err));
        StringAssert.Contains(err, "'1' is not a letter");

        Assert.IsFalse(ShellHotkeyLetters.TryNormalize(";", out _, out err));
        Assert.IsNotNull(err);

        Assert.IsFalse(ShellHotkeyLetters.TryNormalize("", out _, out err));
        StringAssert.Contains(err, "no letters");

        Assert.IsFalse(ShellHotkeyLetters.TryNormalize(null, out _, out err));
        Assert.IsNotNull(err);
    }

    [TestMethod]
    public void Union_MergesAndKeepsSorted()
    {
        Assert.AreEqual("EQR", ShellHotkeyLetters.Union("QR", "E"));
        Assert.AreEqual("QR", ShellHotkeyLetters.Union("QR", "Q"));
        Assert.AreEqual("Q", ShellHotkeyLetters.Union(null, "Q"));
        Assert.AreEqual("Q", ShellHotkeyLetters.Union("Q", null));
        Assert.AreEqual("", ShellHotkeyLetters.Union(null, null));
    }

    [TestMethod]
    public void Subtract_TakesBackOnlyOurLetters()
    {
        // The whole point of restore: a letter the user set by hand must survive.
        Assert.AreEqual("X", ShellHotkeyLetters.Subtract("QRX", "QR"));
        Assert.AreEqual("", ShellHotkeyLetters.Subtract("QR", "QR"));
        Assert.AreEqual("QR", ShellHotkeyLetters.Subtract("QR", ""));
        Assert.AreEqual("QR", ShellHotkeyLetters.Subtract("QR", null));
        Assert.AreEqual("", ShellHotkeyLetters.Subtract(null, "QR"));
        // Removing something that was never there is not an error.
        Assert.AreEqual("QR", ShellHotkeyLetters.Subtract("QR", "Z"));
    }

    [TestMethod]
    public void Added_ReportsOnlyWhatWouldChange()
    {
        Assert.AreEqual("E", ShellHotkeyLetters.Added("QR", "QRE"));
        Assert.AreEqual("", ShellHotkeyLetters.Added("QR", "Q"));
        Assert.AreEqual("QR", ShellHotkeyLetters.Added(null, "QR"));
    }

    [TestMethod]
    public void Cap_IsTwentyTwoCharacters()
    {
        // Windows reads no further; the command refuses rather than silently truncating.
        Assert.AreEqual(22, ShellHotkeyLetters.MaxLength);
        string all = ShellHotkeyLetters.Union("ABCDEFGHIJKLM", "NOPQRSTUVWXYZ");
        Assert.AreEqual(26, all.Length);
        Assert.IsTrue(all.Length > ShellHotkeyLetters.MaxLength);
    }

    [TestMethod]
    public void RoundTrip_DisableThenRestoreLeavesForeignLettersIntact()
    {
        // User already had X; we add Q and R; restore must leave exactly X.
        const string preexisting = "X";
        Assert.IsTrue(ShellHotkeyLetters.TryNormalize("qr", out string wanted, out _));
        string added = ShellHotkeyLetters.Added(preexisting, wanted);
        string merged = ShellHotkeyLetters.Union(preexisting, wanted);
        Assert.AreEqual("QR", added);
        Assert.AreEqual("QRX", merged);
        Assert.AreEqual(preexisting, ShellHotkeyLetters.Subtract(merged, added));
    }
}
