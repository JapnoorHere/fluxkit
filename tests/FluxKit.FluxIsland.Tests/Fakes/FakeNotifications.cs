using FluxKit.Tools.FluxIsland.Abstractions;

namespace FluxKit.FluxIsland.Tests.Fakes;

internal sealed class FakeNotifications : INotificationSource
{
    public List<uint> RemovedIds { get; } = [];

    public event EventHandler<ToastInfo>? Arrived;

    public event EventHandler<uint>? Removed;

    public static ToastInfo Toast(uint id, string app = "Teams", string title = "Priya", string body = "Lunch?") =>
        new(id, app + "!App", app, ReadOnlyMemory<byte>.Empty, title, body, DateTimeOffset.UnixEpoch);

    public void Send(ToastInfo toast) => Arrived?.Invoke(this, toast);

    public void Leave(uint id) => Removed?.Invoke(this, id);

    public void Remove(uint id) => RemovedIds.Add(id);

    public void Start()
    {
    }

    public void Dispose()
    {
    }
}
