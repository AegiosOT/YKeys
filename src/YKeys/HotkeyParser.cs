using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace YKeys;

/// <summary>
/// A parsed hotkey: the chord as written (for messages), the RegisterHotKey
/// arguments, and the command line the binding runs.
/// </summary>
internal sealed record HotkeyBinding(string Chord, HOT_KEY_MODIFIERS Modifiers, uint VirtualKey, string CommandLine);

internal static class HotkeyParser
{
    /// <summary>"alt+shift+1" → MOD_ALT|MOD_SHIFT + VK_1. Last token is the key.</summary>
    public static bool TryParse(string chord, out HOT_KEY_MODIFIERS modifiers, out uint vk, out string? error)
    {
        modifiers = 0;
        vk = 0;
        error = null;

        if (string.IsNullOrWhiteSpace(chord))
        {
            error = "empty hotkey";
            return false;
        }

        string[] tokens = chord.ToLowerInvariant().Split('+', StringSplitOptions.TrimEntries);
        if (tokens.Any(string.IsNullOrEmpty))
        {
            error = "empty part (write the '+' key as 'plus')";
            return false;
        }

        for (int i = 0; i < tokens.Length - 1; i++)
        {
            HOT_KEY_MODIFIERS? mod = tokens[i] switch
            {
                "alt" => HOT_KEY_MODIFIERS.MOD_ALT,
                "ctrl" or "control" => HOT_KEY_MODIFIERS.MOD_CONTROL,
                "shift" => HOT_KEY_MODIFIERS.MOD_SHIFT,
                "win" => HOT_KEY_MODIFIERS.MOD_WIN,
                _ => null,
            };
            if (mod is null)
            {
                error = $"unknown modifier '{tokens[i]}'";
                return false;
            }
            if ((modifiers & mod.Value) != 0)
            {
                error = $"modifier '{tokens[i]}' repeated";
                return false;
            }
            modifiers |= mod.Value;
        }

        if (!TryParseKey(tokens[^1], out vk))
        {
            error = $"unknown key '{tokens[^1]}'";
            return false;
        }

        // RegisterHotKey docs: F12 is reserved for debuggers at all times.
        if (vk == (uint)VIRTUAL_KEY.VK_F12)
        {
            error = "f12 is reserved for debuggers";
            return false;
        }

        // A bare key would hijack normal typing system-wide; F13-F24 are the
        // exception since keyboards only emit them as deliberate macro keys.
        bool macroKey = vk is >= (uint)VIRTUAL_KEY.VK_F13 and <= (uint)VIRTUAL_KEY.VK_F24;
        if (modifiers == 0 && !macroKey)
        {
            error = "needs a modifier (alt/ctrl/shift/win); only f13-f24 may bind bare";
            return false;
        }

        return true;
    }

    private static bool TryParseKey(string key, out uint vk)
    {
        vk = 0;
        if (key.Length == 1)
        {
            char c = key[0];
            if (c is >= 'a' and <= 'z')
            {
                vk = (uint)VIRTUAL_KEY.VK_A + (uint)(c - 'a');
                return true;
            }
            if (c is >= '0' and <= '9')
            {
                vk = (uint)VIRTUAL_KEY.VK_0 + (uint)(c - '0');
                return true;
            }
        }

        if (key.Length is 2 or 3 && key[0] == 'f'
            && int.TryParse(key.AsSpan(1), out int f) && f is >= 1 and <= 24)
        {
            vk = (uint)VIRTUAL_KEY.VK_F1 + (uint)(f - 1);
            return true;
        }

        // OEM keys are US-layout positions; named for what the keycap says there.
        vk = (uint)(key switch
        {
            "left" => VIRTUAL_KEY.VK_LEFT,
            "right" => VIRTUAL_KEY.VK_RIGHT,
            "up" => VIRTUAL_KEY.VK_UP,
            "down" => VIRTUAL_KEY.VK_DOWN,
            "space" => VIRTUAL_KEY.VK_SPACE,
            "enter" or "return" => VIRTUAL_KEY.VK_RETURN,
            "tab" => VIRTUAL_KEY.VK_TAB,
            "esc" or "escape" => VIRTUAL_KEY.VK_ESCAPE,
            "backspace" => VIRTUAL_KEY.VK_BACK,
            "delete" or "del" => VIRTUAL_KEY.VK_DELETE,
            "insert" or "ins" => VIRTUAL_KEY.VK_INSERT,
            "home" => VIRTUAL_KEY.VK_HOME,
            "end" => VIRTUAL_KEY.VK_END,
            "pageup" or "pgup" => VIRTUAL_KEY.VK_PRIOR,
            "pagedown" or "pgdn" => VIRTUAL_KEY.VK_NEXT,
            "minus" or "dash" => VIRTUAL_KEY.VK_OEM_MINUS,
            "plus" or "equals" => VIRTUAL_KEY.VK_OEM_PLUS,
            "comma" => VIRTUAL_KEY.VK_OEM_COMMA,
            "period" or "dot" => VIRTUAL_KEY.VK_OEM_PERIOD,
            "semicolon" => VIRTUAL_KEY.VK_OEM_1,
            "slash" => VIRTUAL_KEY.VK_OEM_2,
            "grave" or "backtick" => VIRTUAL_KEY.VK_OEM_3,
            "lbracket" => VIRTUAL_KEY.VK_OEM_4,
            "backslash" => VIRTUAL_KEY.VK_OEM_5,
            "rbracket" => VIRTUAL_KEY.VK_OEM_6,
            "quote" or "apostrophe" => VIRTUAL_KEY.VK_OEM_7,
            _ => (VIRTUAL_KEY)0,
        });
        return vk != 0;
    }
}
