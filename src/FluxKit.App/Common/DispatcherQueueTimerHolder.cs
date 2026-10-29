namespace FluxKit.App.Common;

/// <summary>One-shot UI-thread timers that can all be cancelled together.</summary>
internal sealed class DispatcherQueueTimerHolder(Microsoft.UI.Dispatching.DispatcherQueue queue)
{
    private readonly List<Microsoft.UI.Dispatching.DispatcherQueueTimer> _timers = [];

    public void Once(TimeSpan after, Action action)
    {
        Microsoft.UI.Dispatching.DispatcherQueueTimer timer = queue.CreateTimer();
        timer.Interval = after;
        timer.IsRepeating = false;
        timer.Tick += (t, _) =>
        {
            t.Stop();
            _timers.Remove(t);
            action();
        };
        _timers.Add(timer);
        timer.Start();
    }

    public void StopAll()
    {
        foreach (Microsoft.UI.Dispatching.DispatcherQueueTimer timer in _timers)
        {
            timer.Stop();
        }

        _timers.Clear();
    }
}
