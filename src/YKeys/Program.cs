namespace YKeys;

internal static class Program
{
    private const string Version = "0.1.3-dev";

    private static int Main(string[] args)
    {
        if (args.Contains("--version") || args.Contains("-V"))
        {
            Console.WriteLine($"ykeys {Version}");
            return 0;
        }

        if (args.Contains("--help") || args.Contains("-h"))
        {
            Console.WriteLine(
                $"""
                ykeys {Version} — hotkey daemon for Windows (companion to YTile)

                usage: ykeys [--log]

                Reads ~/.config/ykeys/ykeys.json and registers its "hotkeys" map as
                global hotkeys; each binding runs its command line when pressed.
                The config is re-applied automatically whenever the file changes.

                  --log    write output to %LOCALAPPDATA%\ykeys\ykeys.log instead
                           of the console (used when launched by `ytile start`)
                """);
            return 0;
        }

        // The instance check must precede the log redirect: the first instance
        // holds the log write handle, so a second --log instance would die on
        // the FileStream open before ever reaching this message.
        // Semaphore, not Mutex: no thread affinity, released from any thread.
        using var instanceLock = new Semaphore(1, 1, @"Local\ykeys-instance", out _);
        if (!instanceLock.WaitOne(0))
        {
            Console.Error.WriteLine("ykeys: another instance is already running.");
            return 1;
        }

        // --log: headless mode — everything goes to a file instead of the
        // hidden console. Truncates per session; FileShare.Read allows tailing.
        if (args.Contains("--log"))
        {
            string logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ykeys");
            Directory.CreateDirectory(logDir);
            var log = new StreamWriter(new FileStream(
                Path.Combine(logDir, "ykeys.log"),
                FileMode.Create, FileAccess.Write, FileShare.Read))
            { AutoFlush = true };
            Console.SetOut(log);
            Console.SetError(log);
        }

        YKeysConfig config = YKeysConfig.Load(null, out string? configError);
        Console.WriteLine(File.Exists(YKeysConfig.DefaultPath)
            ? $"config: {YKeysConfig.DefaultPath}"
            : $"config: no file at {YKeysConfig.DefaultPath} — no hotkeys until one exists");
        if (configError is not null)
        {
            Console.WriteLine($"config problems: {configError}");
        }

        Console.WriteLine($"ykeys {Version} — Ctrl+C to exit.");
        HotkeyListener.Start(config.Hotkeys);

        using FileSystemWatcher watcher = WatchConfig();

        using var done = new ManualResetEventSlim();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            done.Set();
        };
        done.Wait();

        HotkeyListener.Stop();
        Thread.Sleep(100);
        return 0;
    }

    /// <summary>Re-applies the config whenever the file changes. Editors fire
    /// bursts of Changed/Renamed events, so reloads are debounced.</summary>
    private static FileSystemWatcher WatchConfig()
    {
        string dir = Path.GetDirectoryName(YKeysConfig.DefaultPath)!;
        Directory.CreateDirectory(dir);

        var debounce = new System.Timers.Timer(300) { AutoReset = false };
        debounce.Elapsed += (_, _) =>
        {
            YKeysConfig config = YKeysConfig.Load(null, out string? error);
            Console.WriteLine(error is null
                ? $"config reloaded ({config.Hotkeys.Count} hotkeys)"
                : $"config reloaded with problems: {error}");
            HotkeyListener.Apply(config.Hotkeys);
        };

        var watcher = new FileSystemWatcher(dir, Path.GetFileName(YKeysConfig.DefaultPath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        FileSystemEventHandler onChange = (_, _) => { debounce.Stop(); debounce.Start(); };
        watcher.Changed += onChange;
        watcher.Created += onChange;
        // Deletion must unregister everything, matching what a fresh start
        // with no config file would do.
        watcher.Deleted += onChange;
        watcher.Renamed += (_, _) => { debounce.Stop(); debounce.Start(); };
        watcher.EnableRaisingEvents = true;
        return watcher;
    }
}
