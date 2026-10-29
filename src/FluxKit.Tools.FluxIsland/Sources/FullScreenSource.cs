using FluxKit.Platform.Watchers;
using FluxKit.Tools.FluxIsland.Abstractions;
using Serilog;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary><see cref="IFullScreenSource"/> backed by the Platform <see cref="FullScreenWatcher"/>.</summary>
internal sealed class FullScreenSource : IFullScreenSource
{
    private readonly FullScreenWatcher _watcher;

    public FullScreenSource(ILogger log)
    {
        _watcher = new FullScreenWatcher(log);
        _watcher.Changed += (_, e) => Changed?.Invoke(this, e);
    }

    public bool IsFullScreen => _watcher.IsFullScreen;

    public event EventHandler? Changed;

    public void Start() => _watcher.Start();

    public void Dispose() => _watcher.Dispose();
}
