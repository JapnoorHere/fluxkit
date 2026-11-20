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
    private BannerControl? _bannerControl;

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

    /// <summary>
    /// Starts reading notifications if it is turned on but couldn't start before, because Windows' permission is asked
    /// (on the UI thread) only after the island has started.
    /// </summary>
    public void ResumeNotifications()
    {
        if (_view is null || !_options.ShowNotifications || _notifications is not null)
        {
            return;
        }

        StartNotifications();
        ApplyBanners();
        Publish();
    }

    private void StartNotifications() =>
        _notifications = Start(_options.ShowNotifications ? _factories.Notifications : null, s =>
        {
            s.Arrived += OnNotification;
            s.Removed += OnNotificationRemoved;
        });

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
        return added;
    }

    /// <summary>The apps whose Windows banner is off because the island shows their notifications.</summary>
    internal IReadOnlyCollection<string> HiddenBanners => _bannerControl?.Hidden ?? [];

    /// <summary>
    /// Turns Windows' banner off for each app the island shows (and that the user left "Hide Windows banner" on for),
    /// and back on for every other app FluxKit changed. Only while notifications are actually being read: without the
    /// listener, hiding banners would lose notifications.
    /// </summary>
    private void ApplyBanners()
    {
        if (_factories.Banners is not { } banners)
        {
            return;
        }

        _bannerControl ??= new BannerControl(banners, _factories.BannerStore ?? new MemoryBannerStore());
        IEnumerable<string> wanted = _options.ShowNotifications && _notifications is not null
            ? _options.NotificationApps.Where(app => app.Value.Show && app.Value.HideBanner).Select(app => app.Key)
            : [];
        _bannerControl.Apply(wanted);
    }

    private void RestoreBanners() => _bannerControl?.RestoreAll();

    private void OnNotificationSettingsChanged(FluxIslandSettings before)
    {
        if (before.ShowNotifications && !_options.ShowNotifications)
        {
            // Turning the feature off forgets what was read.
            lock (_sync)
            {
                _inbox.Clear();
            }
        }

        ApplyBanners();
    }

    /// <summary>Keeps the banner record in memory only (tests, or no store given).</summary>
    private sealed class MemoryBannerStore : IBannerSnapshotStore
    {
        private Dictionary<string, int?> _saved = [];

        public IReadOnlyDictionary<string, int?> Load() => _saved;

        public void Save(IReadOnlyDictionary<string, int?> saved) => _saved = new(saved);
    }
}
