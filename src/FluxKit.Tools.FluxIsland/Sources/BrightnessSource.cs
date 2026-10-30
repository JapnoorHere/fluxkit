using FluxKit.Platform.Watchers;
using FluxKit.Tools.FluxIsland.Abstractions;
using Serilog;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary><see cref="IBrightnessSource"/> backed by the Platform <see cref="BrightnessWatcher"/>.</summary>
internal sealed class BrightnessSource : IBrightnessSource
{
    private readonly BrightnessWatcher _watcher;

    public BrightnessSource(ILogger log)
    {
        _watcher = new BrightnessWatcher(log);
        _watcher.Changed += (_, level) => Changed?.Invoke(this, level);
    }

    public event EventHandler<int>? Changed;

    public void Start() => _watcher.Start();

    public void Dispose() => _watcher.Dispose();
}
