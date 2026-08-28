using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace YKeys;

/// <summary>
/// Global hotkeys via RegisterHotKey on a dedicated hidden-window thread.
/// WM_HOTKEY hands the binding's command line to CommandRunner and returns —
/// the pump never blocks on a spawn. RegisterHotKey/UnregisterHotKey are
/// thread-affine, which is why config reloads hand new bindings over with a
/// thread message instead of registering from the watcher thread.
/// </summary>
internal static unsafe class HotkeyListener
{
    private const string ClassName = "ykeys";
    private const uint ApplyBindingsMessage = PInvoke.WM_APP + 1;
    private const int ErrorHotkeyAlreadyRegistered = 1409; // WIN32 ERROR_HOTKEY_ALREADY_REGISTERED

    private static volatile uint s_threadId;
    private static IReadOnlyList<HotkeyBinding>? s_pending;

    // Touched only on the pump thread (WndProc runs there too) — no locking.
    private static readonly Dictionary<int, HotkeyBinding> s_registered = [];

    // Ids are never reused across applies: a WM_HOTKEY already posted for an
    // old registration must miss the lookup, not resolve to a new binding.
    private static int s_nextId = 1;

    public static void Start(IReadOnlyList<HotkeyBinding> bindings)
    {
        Interlocked.Exchange(ref s_pending, bindings);
        var thread = new Thread(ThreadProc) { Name = "ykeys-pump", IsBackground = true };
        thread.Start();
    }

    /// <summary>Swap the registered set (config reload). Safe from any thread.</summary>
    public static void Apply(IReadOnlyList<HotkeyBinding> bindings)
    {
        Interlocked.Exchange(ref s_pending, bindings);
        uint id = s_threadId;
        if (id != 0)
        {
            PInvoke.PostThreadMessage(id, ApplyBindingsMessage, default, default);
        }
    }

    public static void Stop()
    {
        uint id = s_threadId;
        if (id != 0)
        {
            PInvoke.PostThreadMessage(id, PInvoke.WM_QUIT, default, default);
        }
    }

    private static void ThreadProc()
    {
        s_threadId = PInvoke.GetCurrentThreadId();

        var hInstance = (HINSTANCE)PInvoke.GetModuleHandle(default(PCWSTR)).Value;
        fixed (char* className = ClassName)
        {
            var wc = new WNDCLASSW
            {
                lpfnWndProc = &WndProc,
                hInstance = hInstance,
                lpszClassName = className,
            };
            PInvoke.RegisterClass(&wc);

            HWND hwnd = PInvoke.CreateWindowEx(
                0, className, className, 0, 0, 0, 0, 0, HWND.Null, HMENU.Null, hInstance, null);
            if (hwnd.IsNull)
            {
                // Thread IDs are recycled system-wide: a stale s_threadId would
                // let Stop() post WM_QUIT into some unrelated process's pump.
                s_threadId = 0;
                Log("hotkeys: window creation failed — hotkeys disabled");
                return;
            }

            ApplyPending(hwnd);

            while (PInvoke.GetMessage(out MSG msg, default, 0, 0))
            {
                // Posted thread messages never reach a WndProc via DispatchMessage.
                if (msg.message == ApplyBindingsMessage)
                {
                    ApplyPending(hwnd);
                    continue;
                }
                PInvoke.TranslateMessage(in msg);
                PInvoke.DispatchMessage(in msg);
            }

            UnregisterAll(hwnd);
            PInvoke.DestroyWindow(hwnd);
            PInvoke.UnregisterClass(className, hInstance);
            s_threadId = 0;
        }
    }

    private static void ApplyPending(HWND hwnd)
    {
        IReadOnlyList<HotkeyBinding>? bindings = Interlocked.Exchange(ref s_pending, null);
        if (bindings is null)
        {
            return;
        }

        UnregisterAll(hwnd);
        int registered = 0;
        foreach (HotkeyBinding binding in bindings)
        {
            // Application hotkey ids must stay <= 0xBFFF.
            if (s_nextId > 0xBFFF)
            {
                s_nextId = 1;
            }
            // MOD_NOREPEAT: a held combo must not spawn a process per repeat.
            if (PInvoke.RegisterHotKey(hwnd, s_nextId, binding.Modifiers | HOT_KEY_MODIFIERS.MOD_NOREPEAT, binding.VirtualKey))
            {
                s_registered[s_nextId] = binding;
                s_nextId++;
                registered++;
            }
            else
            {
                int err = Marshal.GetLastPInvokeError();
                Log(err == ErrorHotkeyAlreadyRegistered
                    ? $"hotkey '{binding.Chord}' is taken by another program (whkd? Windows itself?) — skipped"
                    : $"hotkey '{binding.Chord}' failed to register (error {err})");
            }
        }

        Log($"hotkeys: {registered}/{bindings.Count} registered");
    }

    private static void UnregisterAll(HWND hwnd)
    {
        foreach (int id in s_registered.Keys)
        {
            PInvoke.UnregisterHotKey(hwnd, id);
        }
        s_registered.Clear();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        try
        {
            if (msg == PInvoke.WM_HOTKEY && s_registered.TryGetValue((int)wParam.Value, out HotkeyBinding? binding))
            {
                CommandRunner.Run(binding.Chord, binding.CommandLine);
            }
        }
        catch
        {
            // Never throw across the native callback boundary.
        }

        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private static void Log(string message) => Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {message}");
}
