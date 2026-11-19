using System.Runtime.InteropServices;
using Serilog;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace FluxKit.Platform.Watchers;

/// <summary>One Windows notification, as plain data. Its text is shown on screen only and never logged.</summary>
public sealed record WindowsNotification(uint Id, string AppId, string AppName, ReadOnlyMemory<byte> Logo, string Title, string Body, DateTimeOffset Arrived);

/// <summary>
/// Reads other apps' notifications through Windows' notification listener (ADR-014). It needs FluxKit's package
/// identity and the user's permission. Windows tells us as notifications arrive and leave; nothing polls. Logs carry
/// app ids and counts only, never a notification's text.
/// </summary>
public sealed class NotificationWatcher(ILogger log) : IDisposable
{
    private const int LogoSize = 64;

    private readonly ILogger _log = log.ForContext<NotificationWatcher>();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ReadOnlyMemory<byte>> _logos = new(StringComparer.OrdinalIgnoreCase);
    private UserNotificationListener? _listener;

    /// <summary>A notification arrived. Raised on a thread-pool thread.</summary>
    public event EventHandler<WindowsNotification>? Arrived;

    /// <summary>A notification left Notification Center (dismissed there, or by its app).</summary>
    public event EventHandler<uint>? Removed;

    /// <summary>Whether the user has allowed FluxKit to read notifications. False without the identity package.</summary>
    public static bool IsAllowed
    {
        get
        {
            try
            {
                return UserNotificationListener.Current.GetAccessStatus() == UserNotificationListenerAccessStatus.Allowed;
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    /// <summary>Asks Windows for permission (it may show its own prompt). Must be called from the UI thread.</summary>
    public static async Task<bool> RequestAccessAsync()
    {
        try
        {
            return await UserNotificationListener.Current.RequestAccessAsync() == UserNotificationListenerAccessStatus.Allowed;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Start()
    {
        if (!IsAllowed)
        {
            throw new InvalidOperationException("Notification access isn't allowed (no identity or no permission).");
        }

        _listener = UserNotificationListener.Current;
        _listener.NotificationChanged += OnChanged;
    }

    /// <summary>Removes a notification from Notification Center too (Dismiss on the island).</summary>
    public void Remove(uint id)
    {
        try
        {
            _listener?.RemoveNotification(id);
        }
        catch (Exception ex) when (ex is COMException or ArgumentException)
        {
            _log.Debug(ex, "Removing notification {Id} failed", id);
        }
    }

    public void Dispose()
    {
        if (_listener is not null)
        {
            _listener.NotificationChanged -= OnChanged;
            _listener = null;
        }
    }

    private async void OnChanged(UserNotificationListener sender, UserNotificationChangedEventArgs e)
    {
        try
        {
            if (e.ChangeKind == UserNotificationChangedKind.Removed)
            {
                Removed?.Invoke(this, e.UserNotificationId);
                return;
            }

            if (sender.GetNotification(e.UserNotificationId) is not { } notification)
            {
                return;
            }

            WindowsNotification? read = await ReadAsync(notification);
            if (read is not null)
            {
                _log.Debug("Notification from {App}", read.AppId);
                Arrived?.Invoke(this, read);
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or ArgumentException)
        {
            _log.Debug(ex, "Reading a notification failed");
        }
    }

    private async Task<WindowsNotification?> ReadAsync(UserNotification notification)
    {
        NotificationBinding? binding = notification.Notification.Visual?.GetBinding(KnownNotificationBindings.ToastGeneric);
        if (binding is null)
        {
            return null;
        }

        // ToastGeneric: the first text is the title, the rest the body.
        List<string> texts = [.. binding.GetTextElements().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t))];
        string appId = notification.AppInfo.AppUserModelId ?? string.Empty;
        string appName = notification.AppInfo.DisplayInfo?.DisplayName ?? appId;
        return new WindowsNotification(
            notification.Id,
            appId,
            appName,
            await LogoAsync(notification),
            texts.Count > 0 ? texts[0] : string.Empty,
            string.Join(Environment.NewLine, texts.Skip(1)),
            notification.CreationTime);
    }

    /// <summary>The app's logo as image bytes, read once per app.</summary>
    private async Task<ReadOnlyMemory<byte>> LogoAsync(UserNotification notification)
    {
        string appId = notification.AppInfo.AppUserModelId ?? string.Empty;
        if (_logos.TryGetValue(appId, out ReadOnlyMemory<byte> cached))
        {
            return cached;
        }

        ReadOnlyMemory<byte> logo = ReadOnlyMemory<byte>.Empty;
        try
        {
            if (notification.AppInfo.DisplayInfo?.GetLogo(new Size(LogoSize, LogoSize)) is { } reference)
            {
                using IRandomAccessStreamWithContentType stream = await reference.OpenReadAsync();
                byte[] bytes = new byte[stream.Size];
                using var reader = new DataReader(stream);
                await reader.LoadAsync((uint)stream.Size);
                reader.ReadBytes(bytes);
                logo = bytes;
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or UnauthorizedAccessException or FileNotFoundException)
        {
            _log.Debug(ex, "No logo for {App}", appId);
        }

        _logos[appId] = logo;
        return logo;
    }
}
