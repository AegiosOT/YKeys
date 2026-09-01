using System.Diagnostics;

namespace YKeys;

/// <summary>
/// Runs a binding's command line as a detached process. There is no shell in
/// between, so no persistent shell state and nothing to respawn — but a command
/// may still ask for one explicitly (<c>powershell -NoProfile -Command "a; b"</c>
/// passes through the quote-aware split unchanged). Quote the program path if it
/// contains spaces.
/// </summary>
internal static class CommandRunner
{
    /// <summary>Fire-and-forget; spawn happens off the pump thread.</summary>
    public static void Run(string chord, string commandLine)
    {
        Task.Run(() =>
        {
            (string file, string args) = Split(commandLine);
            file = Resolve(file);
            try
            {
                using Process? proc = Process.Start(new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = args,
                    UseShellExecute = false,
                    // Console children must not flash a window; GUI children ignore this.
                    CreateNoWindow = true,
                });
                if (proc is null)
                {
                    Log($"hotkey '{chord}': '{file}' did not start");
                }
            }
            catch (Exception ex)
            {
                Log($"hotkey '{chord}': cannot run '{file}' — {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Prefer a sibling executable over whatever PATH resolves to. ykeys ships
    /// beside ytile.exe, so suite bindings keep working when PATH is stale or
    /// the install moved — the failure mode that silently kills every binding
    /// at once and looks like the daemon is broken.
    /// </summary>
    internal static string Resolve(string file)
    {
        // Only bare names: an explicit path is the user being specific.
        if (file.Length == 0 || file.Contains('\\') || file.Contains('/') || file.Contains(':'))
        {
            return file;
        }

        string name = file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? file : file + ".exe";
        string sibling = Path.Combine(AppContext.BaseDirectory, name);
        return File.Exists(sibling) ? sibling : file;
    }

    /// <summary>First token (quote-aware) is the program; the rest is passed through verbatim.</summary>
    internal static (string File, string Args) Split(string commandLine)
    {
        string trimmed = commandLine.Trim();
        if (trimmed.StartsWith('"'))
        {
            int close = trimmed.IndexOf('"', 1);
            if (close > 0)
            {
                return (trimmed[1..close], trimmed[(close + 1)..].TrimStart());
            }
            // Unterminated quote — let CreateProcess report it.
            return (trimmed, string.Empty);
        }

        int space = trimmed.IndexOf(' ');
        return space < 0 ? (trimmed, string.Empty) : (trimmed[..space], trimmed[(space + 1)..].TrimStart());
    }

    private static void Log(string message) => Program.Log(message);
}
