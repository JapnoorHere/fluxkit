using System.Runtime.InteropServices;
using FluxKit.Platform.Threading;
using FluxKit.Platform.Windows;
using Serilog;

namespace FluxKit.Platform.Watchers;

/// <summary>A window being dragged by its title bar: its handle and where it was before the drag began.</summary>
public sealed record WindowMove(nint Window, PixelRect Before);

/// <summary>
/// Tells when the user starts and stops dragging any window (plan D, window stash). An out-of-context WinEvent hook for
/// move/size start and end, on its own message-loop thread: no polling, and no code runs in other processes.
/// </summary>
public sealed partial class WindowMoveWatcher(ILogger log) : IDisposable
{
    private const uint EventMoveSizeStart = 0x000A;
    private const uint EventMoveSizeEnd = 0x000B;
    private const uint WinEventOutOfContext = 0x0000;
    private const uint WinEventSkipOwnProcess = 0x0002;

    private readonly ILogger _log = log.ForContext<WindowMoveWatcher>();
    private readonly MessageLoopThread _loop = new("FluxKit.WindowMoveWatcher");
    private WinEventProc? _callback;
    private nint _hook;
    private WindowMove? _moving;

    private delegate void WinEventProc(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint thread, uint time);

    /// <summary>A drag began. Raised on the watcher thread.</summary>
    public event EventHandler<WindowMove>? Started;

    /// <summary>The drag ended (the window was let go). Raised on the watcher thread.</summary>
    public event EventHandler<WindowMove>? Ended;

    public void Start()
    {
        _callback = OnWinEvent;
        _loop.Start(
            () =>
            {
                _hook = SetWinEventHook(EventMoveSizeStart, EventMoveSizeEnd, 0, _callback, 0, 0, WinEventOutOfContext | WinEventSkipOwnProcess);
                if (_hook == 0)
                {
                    _log.Warning("Window move hook could not be set (error {Error})", Marshal.GetLastPInvokeError());
                }
            },
            () =>
            {
                if (_hook != 0)
                {
                    _ = UnhookWinEvent(_hook);
                    _hook = 0;
                }
            });
    }

    public void Dispose() => _loop.Stop(TimeSpan.FromSeconds(1));

    private void OnWinEvent(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (idObject != 0 || hwnd == 0)
        {
            return;
        }

        if (eventType == EventMoveSizeStart && GetWindowRect(hwnd, out RECT rect))
        {
            _moving = new WindowMove(hwnd, new PixelRect(rect.Left, rect.Top, rect.Right, rect.Bottom));
            Started?.Invoke(this, _moving);
        }
        else if (eventType == EventMoveSizeEnd && _moving is { } moving && moving.Window == hwnd)
        {
            _moving = null;
            Ended?.Invoke(this, moving);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

#pragma warning disable SYSLIB1054 // Delegate marshalling requires classic DllImport.
    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint module, WinEventProc callback, uint process, uint thread, uint flags);
#pragma warning restore SYSLIB1054

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWinEvent(nint hook);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint window, out RECT rect);
}
