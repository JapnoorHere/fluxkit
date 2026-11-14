using System.Runtime.InteropServices;
using FluxKit.Platform.Native;
using FluxKit.Platform.Threading;
using Serilog;
using static FluxKit.Platform.Native.Kernel32;
using static FluxKit.Platform.Native.User32;

namespace FluxKit.Platform.Watchers;

/// <summary>
/// Registers one system-wide shortcut with <c>RegisterHotKey</c> on a hidden message-only window and raises
/// <see cref="Pressed"/> each time it is pressed. Windows tells us only about this one key combination; no other
/// keys are seen. If another app already owns the combination, registering fails and the shortcut simply does nothing.
/// </summary>
public sealed partial class HotkeyWatcher : IDisposable
{
    public const uint ModAlt = 0x1;
    public const uint ModControl = 0x2;
    public const uint ModShift = 0x4;
    public const uint ModWin = 0x8;
    private const uint ModNoRepeat = 0x4000;
    private const uint WmHotkey = 0x0312;
    private const int HotkeyId = 0x464B; // "FK"
    private const string ClassName = "FluxKit.HotkeyWatcher";
    private static readonly nint HwndMessage = -3;

    private readonly ILogger _log;
    private readonly uint _modifiers;
    private readonly uint _key;
    private readonly MessageLoopThread _loop = new("FluxKit.HotkeyWatcher");
    private readonly WindowProc _proc;
    private nint _hwnd;

    /// <param name="modifiers">A mix of <see cref="ModAlt"/>, <see cref="ModControl"/>, <see cref="ModShift"/> and <see cref="ModWin"/>.</param>
    /// <param name="key">The virtual-key code.</param>
    public HotkeyWatcher(uint modifiers, uint key, ILogger log)
    {
        _log = log.ForContext<HotkeyWatcher>();
        _modifiers = modifiers;
        _key = key;
        _proc = OnMessage;
    }

    private delegate nint WindowProc(nint hwnd, uint msg, nint wParam, nint lParam);

    /// <summary>Raised on the watcher thread each time the shortcut is pressed.</summary>
    public event EventHandler? Pressed;

    /// <summary>False when another app already uses the shortcut.</summary>
    public bool IsRegistered { get; private set; }

    public void Start() => _loop.Start(CreateListener, DestroyListener);

    public void Dispose() => _loop.Stop(TimeSpan.FromSeconds(1));

    private void CreateListener()
    {
        nint module = GetModuleHandle(null);
        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc),
            hInstance = module,
            lpszClassName = ClassName,
        };
        _ = RegisterClassEx(ref wc);
        _hwnd = CreateWindowEx(0, ClassName, string.Empty, 0, 0, 0, 0, 0, HwndMessage, 0, module, 0);
        IsRegistered = _hwnd != 0 && RegisterHotKey(_hwnd, HotkeyId, _modifiers | ModNoRepeat, _key);
        if (!IsRegistered)
        {
            _log.Information("The Flux Island shortcut is taken by another app (error {Error})", Marshal.GetLastPInvokeError());
        }
    }

    private void DestroyListener()
    {
        if (_hwnd != 0)
        {
            if (IsRegistered)
            {
                _ = UnregisterHotKey(_hwnd, HotkeyId);
                IsRegistered = false;
            }

            _ = DestroyWindow(_hwnd);
            _hwnd = 0;
        }

        _ = UnregisterClass(ClassName, GetModuleHandle(null));
    }

    private nint OnMessage(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WmHotkey && wParam == HotkeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            return 0;
        }

        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(nint hwnd, int id);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    private static partial nint DefWindowProc(nint hwnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "UnregisterClassW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterClass(string className, nint instance);
}
