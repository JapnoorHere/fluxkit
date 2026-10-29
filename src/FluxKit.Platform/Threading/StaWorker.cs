using System.Collections.Concurrent;
using FluxKit.Platform.Native;

namespace FluxKit.Platform.Threading;

/// <summary>
/// A dedicated single-threaded-apartment thread with a serial work queue.
/// All shell COM objects live here so apartment rules are never violated.
/// </summary>
public sealed class StaWorker : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;

    public StaWorker(string name)
    {
        _thread = new Thread(Run) { IsBackground = true, Name = name };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public bool IsCurrentThread => Environment.CurrentManagedThreadId == _thread.ManagedThreadId;

    public Task<T> InvokeAsync<T>(Func<T> work, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_queue.TryAdd(() =>
        {
            try
            {
                tcs.TrySetResult(work());
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }))
        {
            tcs.TrySetException(new ObjectDisposedException(nameof(StaWorker)));
        }

        return tcs.Task.WaitAsync(timeout, cancellationToken);
    }

    public Task InvokeAsync(Action work, TimeSpan timeout, CancellationToken cancellationToken = default) =>
        InvokeAsync(() =>
        {
            work();
            return true;
        }, timeout, cancellationToken);

    public void Dispose()
    {
        _queue.CompleteAdding();
        if (!IsCurrentThread)
        {
            _thread.Join(TimeSpan.FromSeconds(2));
        }

        _queue.Dispose();
    }

    private void Run()
    {
        _ = NativeMethods.CoInitializeEx(0, NativeMethods.COINIT_APARTMENTTHREADED);
        try
        {
            foreach (Action work in _queue.GetConsumingEnumerable())
            {
                work();
            }
        }
        catch (ObjectDisposedException)
        {
            // Disposed while draining; nothing left to run.
        }
        finally
        {
            NativeMethods.CoUninitialize();
        }
    }
}
