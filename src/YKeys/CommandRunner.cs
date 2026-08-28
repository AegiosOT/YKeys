using System.Diagnostics;

namespace YKeys;

/// <summary>
/// Runs a binding's command line as a detached process — no shell in between,
/// so there is no persistent shell state and nothing to respawn. CreateProcess
/// resolves the executable against PATH; quote the path if it has spaces.
/// </summary>
internal static class CommandRunner
{
    /// <summary>Fire-and-forget; spawn happens off the pump thread.</summary>
    public static void Run(string chord, string commandLine)
    {
        Task.Run(() =>
        {
            (string file, string args) = Split(commandLine);
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

    private static void Log(string message) => Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {message}");
}
