using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace YKeys;

internal sealed record ConfigDto(Dictionary<string, string>? Hotkeys);

// AllowDuplicateProperties = false: a duplicated chord (or any repeated key)
// must be a reported config error, not a silent last-one-wins.
// ReadCommentHandling = Skip: bindings get parked behind a comment while people
// try layouts out, and commenting a chord back to Windows for a moment is the
// obvious gesture. JSON forbids comments; a hotkey config is not a data format.
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    AllowDuplicateProperties = false,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(ConfigDto))]
internal sealed partial class ConfigJsonContext : JsonSerializerContext;

/// <summary>
/// Loaded at startup and re-loaded whenever the file changes — swapped
/// atomically into the listener, no half-applied binding sets.
/// </summary>
internal sealed class YKeysConfig
{
    public IReadOnlyList<HotkeyBinding> Hotkeys { get; private init; } = [];

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "ykeys", "ykeys.json");

    /// <summary>Never throws: parse problems land in <paramref name="error"/> and defaults apply.</summary>
    public static YKeysConfig Load(string? path, out string? error)
        => Load(path, out error, out _);

    /// <summary>
    /// <paramref name="fileLevelFailure"/> distinguishes "the file could not be
    /// read or parsed at all" from "some bindings were bad". Callers keep the
    /// bindings they already have in the first case: one stray comma should not
    /// silently unregister everything.
    /// </summary>
    public static YKeysConfig Load(string? path, out string? error, out bool fileLevelFailure)
    {
        error = null;
        fileLevelFailure = false;
        path ??= DefaultPath;
        if (!File.Exists(path))
        {
            return new YKeysConfig();
        }

        ConfigDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize(File.ReadAllText(path), ConfigJsonContext.Default.ConfigDto);
        }
        // Deliberately broad: the contract above is "never throws", and an
        // enumerated list has already had to be widened once. A config file is
        // not worth taking the daemon down for, whatever the failure is.
        catch (Exception ex)
        {
            error = $"{path}: {ex.Message}";
            fileLevelFailure = true;
            return new YKeysConfig();
        }

        if (dto is null)
        {
            error = $"{path}: file is empty or contains only 'null'";
            fileLevelFailure = true;
            return new YKeysConfig();
        }

        // A file with no "hotkeys" object at all is almost always a mistake —
        // a typo in the key, or a hand-written file that never had it. Loading
        // it as zero bindings and reporting success is the worst possible
        // failure: everything stops working and nothing says why.
        if (dto.Hotkeys is null)
        {
            error = $"{path}: no \"hotkeys\" object — check the spelling; nothing is bound without it";
            return new YKeysConfig();
        }

        var problems = new List<string>();
        var hotkeys = new List<HotkeyBinding>();
        var seenChords = new HashSet<(HOT_KEY_MODIFIERS, uint)>();
        foreach ((string chord, string command) in dto.Hotkeys)
        {
            if (!HotkeyParser.TryParse(chord, out HOT_KEY_MODIFIERS mods, out uint vk, out string? hotkeyError))
            {
                problems.Add($"bad hotkey '{chord}': {hotkeyError}");
                continue;
            }
            if (string.IsNullOrWhiteSpace(command))
            {
                problems.Add($"hotkey '{chord}' has no command");
                continue;
            }
            // "shift+alt+1" and "alt+shift+1" are the same registration.
            if (!seenChords.Add((mods, vk)))
            {
                problems.Add($"hotkey '{chord}' duplicates an earlier binding");
                continue;
            }
            string commandLine = command.Trim();
            SignalTarget? signal = null;
            if (SignalSender.IsSignal(commandLine)
                && !SignalSender.TryParse(commandLine, out signal, out string? signalError))
            {
                // Validated here rather than on the first press: a typo in a
                // signal target would otherwise register fine and do nothing
                // at all, which is the failure that looks like a dead chord.
                problems.Add($"hotkey '{chord}': {signalError}");
                continue;
            }
            hotkeys.Add(new HotkeyBinding(chord, mods, vk, commandLine, signal));
        }

        if (problems.Count > 0)
        {
            error = $"{path}: {string.Join("; ", problems)}";
        }

        return new YKeysConfig { Hotkeys = hotkeys };
    }
}
