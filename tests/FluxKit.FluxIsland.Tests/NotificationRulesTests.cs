using FluxKit.FluxIsland.Tests.Fakes;
using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;

namespace FluxKit.FluxIsland.Tests;

public sealed class NotificationRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_notification_becomes_a_moment_with_sender_message_and_app()
    {
        IslandActivity? moment = new NotificationRules().ToMoment(FakeNotifications.Toast(7), Context(Now));

        Assert.NotNull(moment);
        Assert.Equal(ActivityKind.Notification, moment.Kind);
        Assert.Equal("Priya", moment.Text);
        Assert.Equal("Lunch?", moment.Detail);
        Assert.Equal("Teams", moment.Source);
        Assert.Equal("Teams!App", moment.Path);
        Assert.Equal(7, moment.Total);
        Assert.Equal(1, moment.Value);
    }

    [Fact]
    public void A_burst_from_one_app_counts_up_and_restarts_after_a_pause()
    {
        var rules = new NotificationRules();
        rules.ToMoment(FakeNotifications.Toast(1), Context(Now));
        IslandActivity? second = rules.ToMoment(FakeNotifications.Toast(2), Context(Now.AddSeconds(2)));
        IslandActivity? other = rules.ToMoment(FakeNotifications.Toast(3, app: "Mail"), Context(Now.AddSeconds(3)));
        IslandActivity? later = rules.ToMoment(FakeNotifications.Toast(4), Context(Now.AddSeconds(30)));

        Assert.Equal(2, second?.Value);
        Assert.Equal(1, other?.Value);
        Assert.Equal(1, later?.Value);
    }

    [Theory]
    [InlineData(NotificationPrivacy.Full, "Priya", "Lunch?")]
    [InlineData(NotificationPrivacy.SenderOnly, "Priya", null)]
    [InlineData(NotificationPrivacy.Hidden, "Teams", null)]
    public void Privacy_decides_how_much_text_shows(NotificationPrivacy privacy, string text, string? detail)
    {
        IslandActivity? moment = new NotificationRules().ToMoment(FakeNotifications.Toast(1), Context(Now, privacy));

        Assert.Equal(text, moment?.Text);
        Assert.Equal(detail, moment?.Detail);
    }

    [Fact]
    public void Presenting_hides_the_text_when_asked()
    {
        IslandActivity? moment = new NotificationRules().ToMoment(FakeNotifications.Toast(1), Context(Now, presenting: true));

        Assert.Equal("Teams", moment?.Text);
        Assert.Null(moment?.Detail);
    }

    [Fact]
    public void Off_muted_or_empty_notifications_do_not_take_over()
    {
        var rules = new NotificationRules();
        rules.Mute("Mail!App", Now.AddHours(1));

        Assert.Null(rules.ToMoment(FakeNotifications.Toast(1), Context(Now, app: new NotificationAppSettings { Show = false })));
        Assert.Null(rules.ToMoment(FakeNotifications.Toast(2, app: "Mail"), Context(Now)));
        Assert.Null(rules.ToMoment(FakeNotifications.Toast(3, title: "", body: ""), Context(Now)));
        Assert.NotNull(rules.ToMoment(FakeNotifications.Toast(4, app: "Mail"), Context(Now.AddHours(2))));
    }

    [Fact]
    public void A_priority_app_is_flagged()
    {
        IslandActivity? moment = new NotificationRules().ToMoment(FakeNotifications.Toast(1), Context(Now, app: new NotificationAppSettings { Priority = true }));

        Assert.True(moment?.Flag);
    }

    [Fact]
    public void The_inbox_keeps_newest_first_replaces_by_id_and_is_capped()
    {
        var inbox = new NotificationInbox();
        for (uint i = 0; i < NotificationInbox.MaxItems + 5; i++)
        {
            inbox.Add(FakeNotifications.Toast(i));
        }

        inbox.Add(FakeNotifications.Toast(NotificationInbox.MaxItems + 4, title: "Edited"));

        Assert.Equal(NotificationInbox.MaxItems, inbox.Count);
        Assert.Equal("Edited", inbox.Items[0].Title);
        Assert.Single(inbox.Items, t => t.Id == NotificationInbox.MaxItems + 4);
    }

    [Fact]
    public void The_inbox_removes_one_app_and_restores_it()
    {
        var inbox = new NotificationInbox();
        inbox.Add(FakeNotifications.Toast(1));
        inbox.Add(FakeNotifications.Toast(2, app: "Mail"));
        IReadOnlyList<ToastInfo> before = inbox.Items;

        Assert.True(inbox.RemoveApp("teams!app"));
        Assert.Equal(["Mail"], inbox.Items.Select(t => t.AppName));

        inbox.Restore(before);
        Assert.Equal(2, inbox.Count);
    }

    private static NotificationContext Context(DateTimeOffset now, NotificationPrivacy privacy = NotificationPrivacy.Full, bool presenting = false, NotificationAppSettings? app = null) =>
        new(now, privacy, presenting, HideWhilePresenting: true, app ?? new NotificationAppSettings());
}
