using FluxKit.Platform.Watchers;
using FluxKit.Tools.FluxIsland.Abstractions;
using Serilog;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary><see cref="INotificationSource"/> backed by the Platform <see cref="NotificationWatcher"/>.</summary>
internal sealed class NotificationSource : INotificationSource
{
    private readonly NotificationWatcher _watcher;

    public NotificationSource(ILogger log)
    {
        _watcher = new NotificationWatcher(log);
        _watcher.Arrived += (_, n) => Arrived?.Invoke(this, new ToastInfo(n.Id, n.AppId, n.AppName, n.Logo, n.Title, n.Body, n.Arrived));
        _watcher.Removed += (_, id) => Removed?.Invoke(this, id);
    }

    public event EventHandler<ToastInfo>? Arrived;

    public event EventHandler<uint>? Removed;

    public void Remove(uint id) => _watcher.Remove(id);

    public void Start() => _watcher.Start();

    public void Dispose() => _watcher.Dispose();
}
