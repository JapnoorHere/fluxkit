using FluxKit.Platform.Shell;
using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;

namespace FluxKit.Tools.FluxIsland;

/// <summary>
/// Windows notifications on the island (ADR-014): each arriving notification goes to the Notifications page and, if
/// its app is on and not muted, takes over the pill for a moment. What may be shown is decided here, before the view
/// gets it, so a hidden message never reaches the screen. Notification text is never logged.
/// </summary>
public sealed partial class FluxIslandTool
{
    private readonly NotificationRules _notificationRules = new();
    private readonly NotificationInbox _inbox = new();
    private INotificationSource? _notifications;

    public void OpenNotification(uint id)
    {
        ToastInfo? toast;
        lock (_sync)
        {
            toast = _inbox.Find(id);
        }

        // Open the app it came from; if Windows can't, Notification Center still has the original to click.
        if (toast is null || !ShellLauncher.OpenApp(toast.AppId))
        {
            ShellLauncher.OpenNotificationCenter();
        }

        Change(() => _arbiter.Dismiss());
    }

    public void DismissNotification(uint id)
    {
        _notifications?.Remove(id);
        Change(() =>
        {
            _inbox.Remove(id);
            if (_arbiter.Primary is { Kind: ActivityKind.Notification } shown && unchecked((uint)shown.Total) == id)
            {
                _arbiter.Dismiss();
            }
        });
    }

    public void MuteNotificationApp(string appId, TimeSpan duration) =>
        Change(() =>
        {
            _notificationRules.Mute(appId, _time.GetUtcNow() + duration);
            if (_arbiter.Primary is { Kind: ActivityKind.Notification } shown && string.Equals(shown.Path, appId, StringComparison.OrdinalIgnoreCase))
            {
                _arbiter.Dismiss();
            }
        });

    public void ClearInbox()
    {
        IReadOnlyList<ToastInfo> before = _inbox.Items;
        Change(() => _inbox.Clear());
        _undo = () => Change(() => _inbox.Restore(before));
    }

    public void RemoveInboxApp(string appId)
    {
        IReadOnlyList<ToastInfo> before = _inbox.Items;
        Change(() => _inbox.RemoveApp(appId));
        _undo = () => Change(() => _inbox.Restore(before));
    }

    /// <summary>The inbox as the view may show it: trimmed by the privacy setting (and hidden while presenting).</summary>
    private IReadOnlyList<ToastInfo> InboxForScene()
    {
        if (!_options.ShowNotifications)
        {
            return [];
        }

        NotificationPrivacy privacy = NotificationRules.Effective(Context(new NotificationAppSettings()));
        return [.. _inbox.Items.Select(t =>
        {
            (string text, string? detail) = NotificationRules.Redact(t.AppName, t.Title, t.Body, privacy);
            return t with { Title = text, Body = detail ?? string.Empty };
        })];
    }

    private NotificationContext Context(NotificationAppSettings app) => new(
        _time.GetUtcNow(),
        _options.NotificationPrivacy,
        _fullScreen?.IsFullScreen == true,
        _options.HideNotificationsWhilePresenting,
        app);

    private void OnNotification(object? sender, ToastInfo toast)
    {
        if (!_options.ShowNotifications)
        {
            return;
        }

        NotificationAppSettings app = AppSettingsFor(toast);
        Change(() =>
        {
            if (!app.Show)
            {
                return;
            }

            _inbox.Add(toast);
            if (_notificationRules.ToMoment(toast, Context(app)) is { } moment)
            {
                RaiseMoment(moment);
            }
        });
    }

    private void OnNotificationRemoved(object? sender, uint id) => Change(() => _inbox.Remove(id));

    /// <summary>
    /// The user's choices for the app that sent <paramref name="toast"/>. An app seen for the first time is added to the
    /// settings (shown, with its banner hidden) so it appears in the per-app list.
    /// </summary>
    private NotificationAppSettings AppSettingsFor(ToastInfo toast)
    {
        if (_options.NotificationApps.TryGetValue(toast.AppId, out NotificationAppSettings? known))
        {
            return known;
        }

        var added = new NotificationAppSettings { Name = toast.AppName };
        _options.NotificationApps[toast.AppId] = added;
        _ = _settings.UpdateAsync<FluxIslandSettings>(FluxIslandSettings.Section, s =>
        {
            s.NotificationApps.TryAdd(toast.AppId, added);
            return s;
        });
        AppAdded?.Invoke(this, toast.AppId);
        return added;
    }

    /// <summary>An app sent its first notification since the feature was turned on (the banner control listens).</summary>
    internal event EventHandler<string>? AppAdded;
}
