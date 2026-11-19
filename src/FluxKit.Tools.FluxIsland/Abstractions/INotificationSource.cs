namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>A Windows notification from another app. Its text is shown on screen only and never logged.</summary>
public sealed record ToastInfo(uint Id, string AppId, string AppName, ReadOnlyMemory<byte> Logo, string Title, string Body, DateTimeOffset Arrived);

/// <summary>Windows notifications as they arrive and leave (ADR-014). Events may arrive on any thread.</summary>
public interface INotificationSource : IIslandSource
{
    event EventHandler<ToastInfo>? Arrived;

    event EventHandler<uint>? Removed;

    /// <summary>Removes a notification from Windows' Notification Center as well.</summary>
    void Remove(uint id);
}
