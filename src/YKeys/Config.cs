using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace YKeys;

internal sealed record ConfigDto(Dictionary<string, string>? Hotkeys);

// AllowDuplicateProperties = false: a duplicated chord (or any repeated key)
// must be a reported config error, not a silent last-one-wins.
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, AllowDuplicateProperties = false)]
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
    {
        error = null;
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
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            error = $"{path}: {ex.Message}";
            return new YKeysConfig();
        }

        if (dto is null)
        {
            return new YKeysConfig();
        }

        var problems = new List<string>();
        var hotkeys = new List<HotkeyBinding>();
        var seenChords = new HashSet<(HOT_KEY_MODIFIERS, uint)>();
        foreach ((string chord, string command) in dto.Hotkeys ?? [])
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
            hotkeys.Add(new HotkeyBinding(chord, mods, vk, command.Trim()));
        }

        if (problems.Count > 0)
        {
            error = $"{path}: {string.Join("; ", problems)}";
        }

        return new YKeysConfig { Hotkeys = hotkeys };
    }
}
