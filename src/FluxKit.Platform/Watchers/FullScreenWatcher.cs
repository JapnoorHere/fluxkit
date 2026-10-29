using System.Runtime.InteropServices;
using FluxKit.Platform.Threading;
using FluxKit.Platform.Windows;
using Serilog;

namespace FluxKit.Platform.Watchers;

/// <summary>
/// Tells when a full-screen app, game or presentation is in front on the primary monitor. Out-of-context WinEvent
/// hooks on its own message-loop thread report foreground changes, and moves of the foreground window (for apps
/// that go full screen in place, like a video in a browser). Nothing is polled and no code runs in other processes.
/// </summary>
public sealed partial class FullScreenWatcher : IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint EventObjectLocationChange = 0x800B;
    private const uint WinEventOutOfContext = 0x0000;
    private const uint WinEventSkipOwnProcess = 0x0002;
    private const int ObjIdWindow = 0;

    private readonly ILogger _log;
    private readonly MessageLoopThread _loop = new("FluxKit.FullScreenWatcher");
    private readonly WinEventProc _callback;
    private nint _foregroundHook;
    private nint _locationHook;
    private uint _watchedProcess;
    private volatile bool _isFullScreen;

    public FullScreenWatcher(ILogger log)
    {
        _log = log.ForContext<FullScreenWatcher>();
        _callback = OnWinEvent;
    }

    private delegate void WinEventProc(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint thread, uint time);

    public bool IsFullScreen => _isFullScreen;

    /// <summary>Raised on the watcher thread when <see cref="IsFullScreen"/> changes.</summary>
    public event EventHandler? Changed;

    public void Start() => _loop.Start(OnLoopStart, OnLoopStop);

    public void Dispose() => _loop.Stop(TimeSpan.FromSeconds(1));

    private void OnLoopStart()
    {
        _foregroundHook = SetWinEventHook(EventSystemForeground, EventSystemForeground, 0, _callback, 0, 0, WinEventOutOfContext | WinEventSkipOwnProcess);
        if (_foregroundHook == 0)
        {
            _log.Warning("Foreground hook could not be set (error {Error})", Marshal.GetLastPInvokeError());
        }

        Evaluate();
    }

    private void OnLoopStop()
    {
        Unhook(ref _locationHook);
        Unhook(ref _foregroundHook);
        _watchedProcess = 0;
    }

    private void OnWinEvent(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (eventType == EventSystemForeground || (idObject == ObjIdWindow && idChild == 0 && hwnd == GetForegroundWindow()))
        {
            Evaluate();
        }
    }

    private void Evaluate()
    {
        nint foreground = GetForegroundWindow();
        WatchMovesOf(WindowHelpers.GetProcessId(foreground));
        bool fullScreen = WindowHelpers.IsFullScreenOnPrimary(foreground) || WindowHelpers.IsFullScreenAppRunning();
        if (fullScreen == _isFullScreen)
        {
            return;
        }

        _isFullScreen = fullScreen;
        _log.Debug("Full-screen app in front: {FullScreen}", fullScreen);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Location events are hooked for the foreground process only, so other apps moving windows cost nothing.</summary>
    private void WatchMovesOf(uint process)
    {
        if (process == _watchedProcess)
        {
            return;
        }

        Unhook(ref _locationHook);
        _watchedProcess = process;
        if (process != 0 && process != Environment.ProcessId)
        {
            _locationHook = SetWinEventHook(EventObjectLocationChange, EventObjectLocationChange, 0, _callback, process, 0, WinEventOutOfContext);
        }
    }

    private static void Unhook(ref nint hook)
    {
        if (hook != 0)
        {
            _ = UnhookWinEvent(hook);
            hook = 0;
        }
    }

#pragma warning disable SYSLIB1054 // Delegate marshalling requires classic DllImport.
    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint module, WinEventProc callback, uint process, uint thread, uint flags);
#pragma warning restore SYSLIB1054

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWinEvent(nint hook);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();
}
