namespace YKeys;

/// <summary>
/// The letter-set algebra behind `ykeys shell-hotkeys`. Windows stores the
/// suppressed shell shortcuts as one string of concatenated uppercase letters
/// (Win+A..Win+Z), capped at 22 characters. Kept pure and free of registry I/O
/// so the rules can be tested directly.
/// </summary>
internal static class ShellHotkeyLetters
{
    /// <summary>Windows reads at most 22 characters from the value.</summary>
    public const int MaxLength = 22;

    /// <summary>
    /// Accepts "qre", "Q,R,E", "Q R E" and the like; yields sorted, de-duplicated
    /// uppercase letters. Anything that is not A-Z is rejected by name rather
    /// than silently dropped — a typo should not quietly disable the wrong key.
    /// </summary>
    public static bool TryNormalize(string? input, out string letters, out string? error)
    {
        letters = string.Empty;
        error = null;

        var seen = new SortedSet<char>();
        foreach (char raw in input ?? string.Empty)
        {
            if (raw is ',' or ' ' or '+' or '-')
            {
                continue;
            }
            char c = char.ToUpperInvariant(raw);
            if (c is < 'A' or > 'Z')
            {
                error = $"'{raw}' is not a letter — only Win+A..Win+Z can be suppressed this way "
                      + "(digits like Win+1 and OEM keys like Win+; cannot)";
                return false;
            }
            seen.Add(c);
        }

        if (seen.Count == 0)
        {
            error = "no letters given";
            return false;
        }

        letters = new string([.. seen]);
        return true;
    }

    /// <summary>Everything in either set, sorted and de-duplicated.</summary>
    public static string Union(string? a, string? b)
    {
        var seen = new SortedSet<char>();
        foreach (char c in (a ?? string.Empty) + (b ?? string.Empty))
        {
            if (c is >= 'A' and <= 'Z')
            {
                seen.Add(c);
            }
        }
        return new string([.. seen]);
    }

    /// <summary>
    /// <paramref name="from"/> minus <paramref name="remove"/>. Used by restore:
    /// only the letters this tool added are taken back, so letters the user set
    /// by hand — before or after — survive untouched.
    /// </summary>
    public static string Subtract(string? from, string? remove)
    {
        var drop = new HashSet<char>(remove ?? string.Empty);
        var kept = new SortedSet<char>();
        foreach (char c in from ?? string.Empty)
        {
            if (c is >= 'A' and <= 'Z' && !drop.Contains(c))
            {
                kept.Add(c);
            }
        }
        return new string([.. kept]);
    }

    /// <summary>Letters present in <paramref name="candidate"/> but not yet in
    /// <paramref name="current"/> — i.e. what a disable would actually add.</summary>
    public static string Added(string? current, string? candidate)
        => Subtract(candidate, current);
}
