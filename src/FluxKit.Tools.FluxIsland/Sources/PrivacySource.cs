using FluxKit.Platform.Watchers;
using FluxKit.Tools.FluxIsland.Abstractions;
using Serilog;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary><see cref="IPrivacySource"/> backed by the Platform <see cref="PrivacyWatcher"/>.</summary>
internal sealed class PrivacySource : IPrivacySource
{
    private readonly PrivacyWatcher _watcher;

    public PrivacySource(ILogger log)
    {
        _watcher = new PrivacyWatcher(log);
        _watcher.Changed += (_, e) => Changed?.Invoke(this, e);
    }

    public PrivacySnapshot Current => new(_watcher.MicrophoneInUse, _watcher.CameraInUse, _watcher.CallInProgress);

    public event EventHandler? Changed;

    public void Start() => _watcher.Start();

    public void Dispose() => _watcher.Dispose();
}
