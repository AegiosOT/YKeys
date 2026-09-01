using System.Diagnostics;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace YKeys;

/// <summary>
/// `ykeys shell-hotkeys` — hands Win+&lt;letter&gt; chords back from the Windows
/// shell so they can be bound here.
///
/// Deliberately a one-shot command and never part of the daemon's lifecycle.
/// The change is a persistent per-user registry setting that only takes effect
/// when the shell restarts, so a daemon could not honestly apply it at start
/// and undo it at stop — Win+E would stay disabled either way. The model is
/// `ytile autostart`: an explicit verb, run when the user means it, cleaned up
/// by the uninstaller.
///
/// What this tool added is recorded separately from what was already there, so
/// restore subtracts only its own letters and leaves hand edits alone.
/// </summary>
internal static class ShellHotkeys
{
    private const string ShellKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string ShellValue = "DisabledHotkeys";
    private const string StateKey = @"Software\YKeys\ShellHotkeys";

    public static int Run(string[] args)
    {
        string verb = args.Length > 0 ? args[0] : "status";
        bool restartShell = args.Contains("--restart-shell");
        string? letters = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--"));

        return verb switch
        {
            "status" => Status(),
            "disable" => Disable(letters, restartShell),
            "restore" => Restore(restartShell),
            _ => Usage(),
        };
    }

    private static int Usage()
    {
        Console.Error.WriteLine(
            """
            usage: ykeys shell-hotkeys <status|disable|restore> [LETTERS] [--restart-shell]

              status              show what Windows currently suppresses, which letters are
                                  ours, and probe which chords are actually free
              disable [LETTERS]   suppress Win+<letter> shortcuts so they can be bound here;
                                  with no LETTERS, uses the win+ chords in your config
              restore             give back only the letters this tool added

            Changes take effect when the shell restarts. Pass --restart-shell to
            restart it now (closes File Explorer windows), or do it yourself later.
            """);
        return 2;
    }

    private static int Status()
    {
        string? current = ReadShellValue();
        string owned = ReadState("Owned") ?? string.Empty;
        string foreign = ShellHotkeyLetters.Subtract(current, owned);

        Console.WriteLine($"DisabledHotkeys : {(current is null ? "(not set)" : current.Length == 0 ? "(empty)" : current)}");
        Console.WriteLine($"  set by ykeys  : {(owned.Length > 0 ? owned : "(none)")}");
        Console.WriteLine($"  set elsewhere : {(foreign.Length > 0 ? foreign : "(none)")}");
        if (ReadState("Baseline") is not null || ReadState("HadBaseline") is not null)
        {
            string had = ReadState("HadBaseline") == "1" ? ReadState("Baseline") ?? string.Empty : "(value absent)";
            Console.WriteLine($"  restores to   : {(had.Length > 0 ? had : "(empty)")}");
        }

        // A live probe is the only honest answer to "is this chord actually mine
        // to take?" — the registry says what Windows was told, not who won.
        string probe = ShellHotkeyLetters.Union(current, ConfigWinLetters());
        if (probe.Length > 0)
        {
            Console.WriteLine();
            Console.WriteLine("chord      state");
            foreach (char c in probe)
            {
                bool free = ProbeFree(c);
                Console.WriteLine($"  win+{char.ToLowerInvariant(c)}    {(free ? "free" : "held by another program")}");
            }
            Console.WriteLine();
            Console.WriteLine("(a running ykeys holds its own bound chords — stop it for a clean reading)");
        }
        return 0;
    }

    private static int Disable(string? requested, bool restartShell)
    {
        string wanted;
        if (requested is null)
        {
            wanted = ConfigWinLetters();
            if (wanted.Length == 0)
            {
                Console.Error.WriteLine(
                    $"ykeys: no win+<letter> chords in {YKeysConfig.DefaultPath} — "
                  + "add some first, or name the letters: ykeys shell-hotkeys disable QRE");
                return 1;
            }
            Console.WriteLine($"letters from your config: {wanted}");
        }
        else if (!ShellHotkeyLetters.TryNormalize(requested, out wanted, out string? error))
        {
            Console.Error.WriteLine($"ykeys: {error}");
            return 2;
        }

        string? current = ReadShellValue();
        string merged = ShellHotkeyLetters.Union(current, wanted);
        if (merged.Length > ShellHotkeyLetters.MaxLength)
        {
            string overflow = merged[ShellHotkeyLetters.MaxLength..];
            Console.Error.WriteLine(
                $"ykeys: Windows reads only {ShellHotkeyLetters.MaxLength} letters and this would need "
              + $"{merged.Length} ({merged}). Drop {overflow} or restore some first.");
            return 1;
        }

        string added = ShellHotkeyLetters.Added(current, wanted);
        if (added.Length == 0)
        {
            Console.WriteLine($"nothing to do — {wanted} already suppressed");
            return 0;
        }

        // Record what was here before our FIRST disable, so restore can tell
        // "delete the value" from "put their content back".
        if (ReadState("HadBaseline") is null)
        {
            WriteState("HadBaseline", current is null ? "0" : "1");
            WriteState("Baseline", current ?? string.Empty);
        }

        WriteShellValue(merged);
        WriteState("Owned", ShellHotkeyLetters.Union(ReadState("Owned"), added));

        Console.WriteLine($"suppressed win+{string.Join(", win+", added.ToLowerInvariant().ToCharArray())}");
        Console.WriteLine($"DisabledHotkeys is now {merged}");
        return Finish(restartShell);
    }

    private static int Restore(bool restartShell)
    {
        string owned = ReadState("Owned") ?? string.Empty;
        if (owned.Length == 0)
        {
            Console.WriteLine("nothing to restore — ykeys has not suppressed any shell hotkeys");
            return 0;
        }

        string? current = ReadShellValue();
        string remaining = ShellHotkeyLetters.Subtract(current, owned);
        bool hadBaseline = ReadState("HadBaseline") == "1";

        if (remaining.Length == 0 && !hadBaseline)
        {
            DeleteShellValue();
            Console.WriteLine("DisabledHotkeys removed");
        }
        else
        {
            WriteShellValue(remaining);
            Console.WriteLine($"DisabledHotkeys is now {(remaining.Length > 0 ? remaining : "(empty)")}");
        }

        ClearState();
        Console.WriteLine($"gave back win+{string.Join(", win+", owned.ToLowerInvariant().ToCharArray())}");
        return Finish(restartShell);
    }

    private static int Finish(bool restartShell)
    {
        if (!restartShell)
        {
            Console.WriteLine("takes effect when the shell restarts (log off, or re-run with --restart-shell)");
            return 0;
        }

        Console.WriteLine("restarting the shell — open File Explorer windows will close...");
        foreach (Process p in Process.GetProcessesByName("explorer"))
        {
            using (p)
            {
                try { p.Kill(); } catch (Exception ex) { Console.Error.WriteLine($"ykeys: {ex.Message}"); }
            }
        }
        // Windows relaunches the shell on its own; only step in if it does not.
        for (int i = 0; i < 30; i++)
        {
            Thread.Sleep(500);
            if (Process.GetProcessesByName("explorer").Length > 0)
            {
                Console.WriteLine("shell is back");
                return 0;
            }
        }
        try { Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true }); } catch { }
        return 0;
    }

    /// <summary>The Win+&lt;letter&gt; chords already bound in the user's config.</summary>
    private static string ConfigWinLetters()
    {
        YKeysConfig config = YKeysConfig.Load(null, out _);
        var letters = new SortedSet<char>();
        foreach (HotkeyBinding b in config.Hotkeys)
        {
            if ((b.Modifiers & HOT_KEY_MODIFIERS.MOD_WIN) != 0
                && b.VirtualKey >= (uint)VIRTUAL_KEY.VK_A
                && b.VirtualKey <= (uint)VIRTUAL_KEY.VK_Z)
            {
                letters.Add((char)b.VirtualKey);
            }
        }
        return new string([.. letters]);
    }

    /// <summary>Can this chord be claimed right now? Registers and immediately
    /// releases, so it never leaves a hotkey behind.</summary>
    private static unsafe bool ProbeFree(char letter)
    {
        const int probeId = 0xBEE;
        bool ok = PInvoke.RegisterHotKey(
            HWND.Null, probeId,
            HOT_KEY_MODIFIERS.MOD_WIN | HOT_KEY_MODIFIERS.MOD_NOREPEAT,
            letter);
        if (ok)
        {
            PInvoke.UnregisterHotKey(HWND.Null, probeId);
        }
        return ok;
    }

    private static string? ReadShellValue()
        => Registry.GetValue($@"HKEY_CURRENT_USER\{ShellKey}", ShellValue, null) as string;

    private static void WriteShellValue(string value)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(ShellKey);
        key.SetValue(ShellValue, value, RegistryValueKind.String);
    }

    private static void DeleteShellValue()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(ShellKey, writable: true);
        key?.DeleteValue(ShellValue, throwOnMissingValue: false);
    }

    private static string? ReadState(string name)
        => Registry.GetValue($@"HKEY_CURRENT_USER\{StateKey}", name, null)?.ToString();

    private static void WriteState(string name, string value)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(StateKey);
        key.SetValue(name, value, RegistryValueKind.String);
    }

    private static void ClearState()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(StateKey, writable: true);
        if (key is null)
        {
            return;
        }
        foreach (string name in new[] { "Owned", "Baseline", "HadBaseline" })
        {
            key.DeleteValue(name, throwOnMissingValue: false);
        }
    }
}
