using Serilog;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// TEMPORARY spike for the notifications plan (phase 1), run with --notification-spike: checks that FluxKit.exe has its
/// package identity, asks for notification access, reads current toasts and listens for changes. Logs counts and app
/// names only, never notification text. Replaced by Platform NotificationWatcher in phase 3.
/// </summary>
internal static class NotificationSpike
{
    public static async Task RunAsync(ILogger log)
    {
        log = log.ForContext(typeof(NotificationSpike));
        string identity;
        try
        {
            identity = Windows.ApplicationModel.Package.Current.Id.FullName;
        }
        catch (InvalidOperationException ex)
        {
            identity = "none (" + ex.HResult.ToString("X8", System.Globalization.CultureInfo.InvariantCulture) + ")";
        }

        log.Information("SPIKE package identity: {Identity}", identity);
        try
        {
            UserNotificationListener listener = UserNotificationListener.Current;
            UserNotificationListenerAccessStatus status = await listener.RequestAccessAsync();
            log.Information("SPIKE listener access: {Status}", status);
            if (status != UserNotificationListenerAccessStatus.Allowed)
            {
                return;
            }

            IReadOnlyList<UserNotification> current = await listener.GetNotificationsAsync(NotificationKinds.Toast);
            log.Information("SPIKE current toasts: {Count}", current.Count);
            foreach (UserNotification n in current.Take(5))
            {
                NotificationBinding? binding = n.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);
                log.Information("SPIKE toast from {App} ({Aumid}) with {Texts} text elements", n.AppInfo.DisplayInfo.DisplayName, n.AppInfo.AppUserModelId, binding?.GetTextElements().Count ?? 0);
            }

            try
            {
                listener.NotificationChanged += (_, e) => log.Information("SPIKE NotificationChanged {Kind} id {Id}", e.ChangeKind, e.UserNotificationId);
                log.Information("SPIKE subscribed to NotificationChanged");
            }
            catch (Exception ex)
            {
                log.Warning(ex, "SPIKE NotificationChanged subscription failed");
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "SPIKE listener failed");
        }
    }
}
