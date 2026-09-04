using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace YKeys;

/// <summary>
/// A parsed hotkey: the chord as written (for messages), the RegisterHotKey
/// arguments, and what the binding does when pressed.
///
/// <para><see cref="Signal"/> is set for an <c>@signal:</c> binding, which pokes
/// an app that is already running instead of starting a process.
/// <see cref="CommandLine"/> is kept either way, because it is what the log
/// prints and what the user actually wrote.</para>
/// </summary>
internal sealed record HotkeyBinding(
    string Chord,
    HOT_KEY_MODIFIERS Modifiers,
    uint VirtualKey,
    string CommandLine,
    SignalTarget? Signal = null);

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
            // Punctuation. The oem_* spellings are what whkd and komorebi
            // configs use, so a whkdrc translates without a lookup table.
            "minus" or "dash" or "oem_minus" => VIRTUAL_KEY.VK_OEM_MINUS,
            "plus" or "equals" or "oem_plus" => VIRTUAL_KEY.VK_OEM_PLUS,
            "comma" or "oem_comma" => VIRTUAL_KEY.VK_OEM_COMMA,
            "period" or "dot" or "oem_period" => VIRTUAL_KEY.VK_OEM_PERIOD,
            "semicolon" or "oem_1" => VIRTUAL_KEY.VK_OEM_1,
            "slash" or "oem_2" => VIRTUAL_KEY.VK_OEM_2,
            "grave" or "backtick" or "oem_3" => VIRTUAL_KEY.VK_OEM_3,
            "lbracket" or "oem_4" => VIRTUAL_KEY.VK_OEM_4,
            "backslash" or "oem_5" => VIRTUAL_KEY.VK_OEM_5,
            "rbracket" or "oem_6" => VIRTUAL_KEY.VK_OEM_6,
            "quote" or "apostrophe" or "oem_7" => VIRTUAL_KEY.VK_OEM_7,
            "oem_8" => VIRTUAL_KEY.VK_OEM_8,
            "oem_102" or "backslash2" => VIRTUAL_KEY.VK_OEM_102,

            // Numpad. Distinct virtual keys from the number row, so a binding
            // on one is not shadowed by the other.
            "numpad0" => VIRTUAL_KEY.VK_NUMPAD0,
            "numpad1" => VIRTUAL_KEY.VK_NUMPAD1,
            "numpad2" => VIRTUAL_KEY.VK_NUMPAD2,
            "numpad3" => VIRTUAL_KEY.VK_NUMPAD3,
            "numpad4" => VIRTUAL_KEY.VK_NUMPAD4,
            "numpad5" => VIRTUAL_KEY.VK_NUMPAD5,
            "numpad6" => VIRTUAL_KEY.VK_NUMPAD6,
            "numpad7" => VIRTUAL_KEY.VK_NUMPAD7,
            "numpad8" => VIRTUAL_KEY.VK_NUMPAD8,
            "numpad9" => VIRTUAL_KEY.VK_NUMPAD9,
            "numpad_add" or "numpad_plus" => VIRTUAL_KEY.VK_ADD,
            "numpad_subtract" or "numpad_minus" => VIRTUAL_KEY.VK_SUBTRACT,
            "numpad_multiply" => VIRTUAL_KEY.VK_MULTIPLY,
            "numpad_divide" => VIRTUAL_KEY.VK_DIVIDE,
            "numpad_decimal" or "numpad_dot" => VIRTUAL_KEY.VK_DECIMAL,

            _ => (VIRTUAL_KEY)0,
        });
        return vk != 0;
    }
}
