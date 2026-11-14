using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Dispatching;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// Thread-safe <see cref="IIslandView"/> for the tool. Scenes can arrive from any thread and in bursts; only the
/// newest one is drawn, once, on the UI thread.
/// </summary>
internal sealed class IslandViewAdapter(IslandWindow window) : IIslandView
{
    private readonly DispatcherQueue _queue = window.DispatcherQueue;
    private IslandScene? _pending;
    private int _queued;

    public void Render(IslandScene scene)
    {
        Volatile.Write(ref _pending, scene);
        if (Interlocked.Exchange(ref _queued, 1) == 0)
        {
            _ = _queue.TryEnqueue(() =>
            {
                Volatile.Write(ref _queued, 0);
                if (Interlocked.Exchange(ref _pending, null) is { } latest)
                {
                    window.RenderCore(latest);
                }
            });
        }
    }

    public void TogglePanel() => _queue.TryEnqueue(window.TogglePanel);

    public void Close() => _queue.TryEnqueue(window.CloseCore);
}
