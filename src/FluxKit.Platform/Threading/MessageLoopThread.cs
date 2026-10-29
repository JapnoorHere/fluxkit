using FluxKit.Platform.Native;
using static FluxKit.Platform.Native.Kernel32;
using static FluxKit.Platform.Native.User32;

namespace FluxKit.Platform.Threading;

/// <summary>
/// A background thread that runs a Win32 message loop, for hooks and hidden windows that need one.
/// <c>onStart</c> runs on the thread before the loop and <c>onStop</c> after it ends; <see cref="Start"/> waits
/// until <c>onStart</c> has finished. The thread can be started again after <see cref="Stop"/>.
/// </summary>
internal sealed class MessageLoopThread(string name, ApartmentState apartment = ApartmentState.MTA)
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(2);

    private Thread? _thread;
    private uint _threadId;

    public bool IsRunning => _thread is not null;

    /// <summary>The Win32 id of the loop thread, or 0 before it has started.</summary>
    public uint ThreadId => _threadId;

    public void Start(Action onStart, Action onStop)
    {
        if (_thread is not null)
        {
            return;
        }

        using var ready = new ManualResetEventSlim(false);
        _thread = new Thread(() => Run(ready, onStart, onStop)) { IsBackground = true, Name = name };
        _thread.SetApartmentState(apartment);
        _thread.Start();
        ready.Wait(StartTimeout);
    }

    /// <summary>Ends the message loop and waits up to <paramref name="timeout"/> for <c>onStop</c> to finish.</summary>
    public void Stop(TimeSpan timeout)
    {
        Thread? thread = _thread;
        if (thread is null)
        {
            return;
        }

        _ = PostThreadMessage(_threadId, WmQuit, 0, 0);
        thread.Join(timeout);
        _thread = null;
    }

    private void Run(ManualResetEventSlim ready, Action onStart, Action onStop)
    {
        _threadId = GetCurrentThreadId();
        try
        {
            onStart();
        }
        finally
        {
            ready.Set();
        }

        while (GetMessage(out MSG msg, 0, 0, 0) > 0)
        {
            _ = TranslateMessage(msg);
            _ = DispatchMessage(msg);
        }

        onStop();
    }
}
