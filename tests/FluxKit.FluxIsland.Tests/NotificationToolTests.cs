using FluxKit.FluxIsland.Tests.Fakes;
using FluxKit.Tools.FluxIsland;
using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.Extensions.Time.Testing;
using Serilog;

namespace FluxKit.FluxIsland.Tests;

public sealed class NotificationToolTests : IAsyncLifetime
{
    private readonly FakeTimeProvider _time = new();
    private readonly FakeSettings _settings = new();
    private readonly FakeIslandView _view = new();
    private readonly FakeNotifications _notifications = new();
    private readonly FakeFullScreen _fullScreen = new();
    private readonly FakeBanners _banners = new();
    private readonly FluxIslandTool _tool;

    public NotificationToolTests()
    {
        var sources = new IslandSources { Notifications = () => _notifications, FullScreen = () => _fullScreen, Banners = _banners, BannerStore = _banners };
        _tool = new FluxIslandTool(_settings, _ => _view, sources, _time, new LoggerConfiguration().CreateLogger());
    }

    public async Task InitializeAsync()
    {
        await SetAsync(s => s.ShowNotifications = true);
        await _tool.StartAsync(CancellationToken.None);
    }

    public async Task DisposeAsync() => await _tool.DisposeAsync();

    [Fact]
    public void An_arriving_notification_shows_and_goes_to_the_inbox()
    {
        _notifications.Send(FakeNotifications.Toast(5));

        Assert.Equal(ActivityKind.Notification, _view.Last.Primary?.Kind);
        Assert.Equal("Priya", _view.Last.Primary?.Text);
        Assert.Single(_view.Last.Inbox);
    }

    [Fact]
    public async Task Nothing_is_read_while_the_feature_is_off()
    {
        await SetAsync(s => s.ShowNotifications = false);

        _notifications.Send(FakeNotifications.Toast(5));

        Assert.Null(_view.Last.Primary);
        Assert.Empty(_view.Last.Inbox);
    }

    [Fact]
    public void A_new_app_is_remembered_in_settings()
    {
        _notifications.Send(FakeNotifications.Toast(1, app: "Mail"));

        FluxIslandSettings saved = _settings.Read<FluxIslandSettings>(FluxIslandSettings.Section);
        Assert.True(saved.NotificationApps.ContainsKey("Mail!App"));
    }

    [Fact]
    public async Task Hidden_privacy_trims_the_inbox_too()
    {
        await SetAsync(s => s.NotificationPrivacy = NotificationPrivacy.Hidden);

        _notifications.Send(FakeNotifications.Toast(1));

        ToastInfo shown = Assert.Single(_view.Last.Inbox);
        Assert.Equal("Teams", shown.Title);
        Assert.Empty(shown.Body);
        Assert.Null(_view.Last.Primary?.Detail);
    }

    [Fact]
    public void Dismiss_removes_it_from_windows_the_inbox_and_the_island()
    {
        _notifications.Send(FakeNotifications.Toast(9));

        _tool.DismissNotification(9);

        Assert.Equal([9u], _notifications.RemovedIds);
        Assert.Empty(_view.Last.Inbox);
        Assert.NotEqual(ActivityKind.Notification, _view.Last.Primary?.Kind);
    }

    [Fact]
    public void A_notification_removed_in_windows_leaves_the_inbox()
    {
        _notifications.Send(FakeNotifications.Toast(3));

        _notifications.Leave(3);

        Assert.Empty(_view.Last.Inbox);
    }

    [Fact]
    public void Muting_an_app_hides_its_moment_but_keeps_the_inbox()
    {
        _tool.MuteNotificationApp("Teams!App", TimeSpan.FromHours(1));

        _notifications.Send(FakeNotifications.Toast(4));

        Assert.Null(_view.Last.Primary);
        Assert.Single(_view.Last.Inbox);
    }

    [Fact]
    public void Clearing_the_inbox_can_be_undone()
    {
        _notifications.Send(FakeNotifications.Toast(1));
        _notifications.Send(FakeNotifications.Toast(2, app: "Mail"));

        _tool.ClearInbox();
        Assert.Empty(_view.Last.Inbox);

        _tool.UndoClear();
        Assert.Equal(2, _view.Last.Inbox.Count);
    }

    [Fact]
    public async Task Only_priority_apps_interrupt_when_set_to_important()
    {
        await SetAsync(s =>
        {
            s.Interruptions = IslandInterruptions.Important;
            s.NotificationApps["VIP!App"] = new NotificationAppSettings { Name = "VIP", Priority = true };
        });

        _notifications.Send(FakeNotifications.Toast(1));
        Assert.Null(_view.Last.Primary);

        _notifications.Send(FakeNotifications.Toast(2, app: "VIP"));
        Assert.Equal(ActivityKind.Notification, _view.Last.Primary?.Kind);
    }

    [Fact]
    public async Task Banners_are_hidden_for_shown_apps_and_restored_when_turned_off_or_stopped()
    {
        _banners.Values["Mail!App"] = 1;
        _notifications.Send(FakeNotifications.Toast(1));
        _notifications.Send(FakeNotifications.Toast(2, app: "Mail"));

        Assert.Equal(0, _banners.Values["Teams!App"]);
        Assert.Equal(0, _banners.Values["Mail!App"]);

        await SetAsync(s => s.NotificationApps["Mail!App"].HideBanner = false);
        Assert.Equal(1, _banners.Values["Mail!App"]);

        await SetAsync(s => s.ShowNotifications = false);
        Assert.Null(_banners.Values["Teams!App"]);
        Assert.Empty(_view.Last.Inbox);

        await SetAsync(s => s.ShowNotifications = true);
        Assert.Equal(0, _banners.Values["Teams!App"]);

        await _tool.StopAsync(CancellationToken.None);
        Assert.Null(_banners.Values["Teams!App"]);
    }

    [Fact]
    public async Task An_app_hidden_from_the_island_keeps_its_windows_banner()
    {
        await SetAsync(s => s.NotificationApps["Mail!App"] = new NotificationAppSettings { Name = "Mail", Show = false });

        _notifications.Send(FakeNotifications.Toast(1, app: "Mail"));

        Assert.False(_banners.Values.ContainsKey("Mail!App"));
        Assert.Empty(_view.Last.Inbox);
    }

    private Task SetAsync(Action<FluxIslandSettings> change) =>
        _settings.UpdateAsync<FluxIslandSettings>(FluxIslandSettings.Section, s =>
        {
            change(s);
            return s;
        });
}
