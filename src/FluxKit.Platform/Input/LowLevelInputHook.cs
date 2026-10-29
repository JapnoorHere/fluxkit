using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Serilog;

namespace FluxKit.Platform.Input;

public enum InputKind
{
    LeftDown,
    LeftUp,
    Move,
    KeyDown,
    KeyUp,
}

/// <summary>A global input event in physical screen pixels.</summary>
public readonly record struct InputEvent(InputKind Kind, int X, int Y, int VirtualKey, long TimestampMs, bool InjectedByFluxKit);

/// <summary>Source of global input events; abstracted so the coordinator can be tested without hooks.</summary>
public interface ISystemInputSource : IDisposable
{
    ChannelReader<InputEvent> Events { get; }

    /// <summary>
    /// Called synchronously on the hook thread for every key-down; return true to swallow the key.
    /// Must be extremely fast and must only read state.
    /// </summary>
    Func<InputEvent, bool>? KeyFilter { get; set; }

    bool IsRunning { get; }

    void Install();

    void Uninstall();
}

/// <summary>
/// WH_MOUSE_LL + WH_KEYBOARD_LL on a dedicated thread with its own message pump.
/// Callbacks only copy the event into a channel and return, staying far below LowLevelHooksTimeout.
/// The hooks observe input without modifying it, except keys explicitly swallowed by <see cref="KeyFilter"/>.
/// </summary>
public sealed partial class LowLevelInputHook : ISystemInputSource
{
    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const int WmQuit = 0x0012;
    private const int WmMouseMove = 0x0200;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonUp = 0x0202;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const long MoveCoalesceMs = 8;

    private readonly ILogger _log;
    private readonly Channel<InputEvent> _channel = Channel.CreateBounded<InputEvent>(
        new BoundedChannelOptions(512) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = true });

    private readonly HookProc _mouseProc;
    private readonly HookProc _keyboardProc;
    private Thread? _thread;
    private uint _threadId;
    private nint _mouseHook;
    private nint _keyboardHook;
    private long _lastMoveMs;

    public LowLevelInputHook(ILogger log)
    {
        _log = log.ForContext<LowLevelInputHook>();
        _mouseProc = MouseCallback;
        _keyboardProc = KeyboardCallback;
    }

    private delegate nint HookProc(int code, nint wParam, nint lParam);

    public ChannelReader<InputEvent> Events => _channel.Reader;

    public Func<InputEvent, bool>? KeyFilter { get; set; }

    public bool IsRunning => _thread is not null;

    public void Install()
    {
        if (_thread is not null)
        {
            return;
        }

        using var ready = new ManualResetEventSlim(false);
        _thread = new Thread(() => Pump(ready)) { IsBackground = true, Name = "FluxKit.InputHook" };
        _thread.Start();
        ready.Wait(TimeSpan.FromSeconds(2));
    }

    public void Uninstall()
    {
        Thread? thread = _thread;
        if (thread is null)
        {
            return;
        }

        _ = PostThreadMessage(_threadId, WmQuit, 0, 0);
        thread.Join(TimeSpan.FromSeconds(2));
        _thread = null;
    }

    public void Dispose()
    {
        Uninstall();
        _channel.Writer.TryComplete();
    }

    private void Pump(ManualResetEventSlim ready)
    {
        _threadId = GetCurrentThreadId();
        nint module = GetModuleHandle(null);
        _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, module, 0);
        _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, module, 0);
        if (_mouseHook == 0 || _keyboardHook == 0)
        {
            _log.Error("Installing low-level hooks failed (error {Error})", Marshal.GetLastPInvokeError());
        }
        else
        {
            _log.Information("Low-level input hooks installed");
        }

        ready.Set();
        while (GetMessage(out MSG msg, 0, 0, 0) > 0)
        {
            _ = TranslateMessage(msg);
            _ = DispatchMessage(msg);
        }

        if (_mouseHook != 0)
        {
            _ = UnhookWindowsHookEx(_mouseHook);
        }

        if (_keyboardHook != 0)
        {
            _ = UnhookWindowsHookEx(_keyboardHook);
        }

        _mouseHook = _keyboardHook = 0;
        _log.Information("Low-level input hooks removed");
    }

    private nint MouseCallback(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            int message = (int)wParam;
            if (message is WmMouseMove or WmLButtonDown or WmLButtonUp)
            {
                var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                long now = Environment.TickCount64;
                if (message != WmMouseMove || now - _lastMoveMs >= MoveCoalesceMs)
                {
                    if (message == WmMouseMove)
                    {
                        _lastMoveMs = now;
                    }

                    InputKind kind = message switch
                    {
                        WmLButtonDown => InputKind.LeftDown,
                        WmLButtonUp => InputKind.LeftUp,
                        _ => InputKind.Move,
                    };
                    _channel.Writer.TryWrite(new InputEvent(kind, data.pt.X, data.pt.Y, 0, now, data.dwExtraInfo == InputSynthesizer.FluxKitSignature));
                }
            }
        }

        return CallNextHookEx(0, code, wParam, lParam);
    }

    private nint KeyboardCallback(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            int message = (int)wParam;
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            bool ours = data.dwExtraInfo == InputSynthesizer.FluxKitSignature;
            if (!ours && message is WmKeyDown or WmSysKeyDown or WmKeyUp or WmSysKeyUp)
            {
                bool down = message is WmKeyDown or WmSysKeyDown;
                var evt = new InputEvent(down ? InputKind.KeyDown : InputKind.KeyUp, 0, 0, (int)data.vkCode, Environment.TickCount64, false);
                bool swallow = false;
                try
                {
                    swallow = KeyFilter?.Invoke(evt) == true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }

                _channel.Writer.TryWrite(evt);
                if (swallow)
                {
                    return 1;
                }
            }
        }

        return CallNextHookEx(0, code, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint hwnd;
        public uint message;
        public nint wParam;
        public nint lParam;
        public uint time;
        public POINT pt;
        public uint lPrivate;
    }

#pragma warning disable SYSLIB1054 // Delegate marshalling requires classic DllImport.
    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, HookProc proc, nint module, uint threadId);
#pragma warning restore SYSLIB1054

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWindowsHookEx(nint hook);

    [LibraryImport("user32.dll")]
    private static partial nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "GetMessageW")]
    private static partial int GetMessage(out MSG msg, nint hwnd, uint min, uint max);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TranslateMessage(in MSG msg);

    [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
    private static partial nint DispatchMessage(in MSG msg);

    [LibraryImport("user32.dll", EntryPoint = "PostThreadMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostThreadMessage(uint threadId, int msg, nint wParam, nint lParam);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, EntryPoint = "GetModuleHandleW")]
    private static partial nint GetModuleHandle(string? name);
}
