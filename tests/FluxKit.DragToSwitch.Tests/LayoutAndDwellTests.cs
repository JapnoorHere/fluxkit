using FluxKit.Platform.Windows;
using FluxKit.Utilities.DragToSwitch;
using FluxKit.Utilities.DragToSwitch.Interaction;

namespace FluxKit.DragToSwitch.Tests;

public sealed class LayoutAndDwellTests
{
    private static readonly PixelRect Work = new(0, 0, 1920, 1040);

    [Fact]
    public void Bottom_center_layout_matches_spec_at_100_percent()
    {
        SelectorGeometry g = SelectorLayout.Compute(3, OverlaySize.Standard, PreviewStyle.Wallpaper, OverlayPosition.BottomCenter, Work, 1.0, 0, 0);

        Assert.Equal(new PixelRect(620, 764, 1300, 1024), g.Overlay);
        Assert.Equal(new PixelRect(636, 820, 844, 1008), g.Cards[0]);
        Assert.Equal(new PixelRect(1076, 820, 1284, 1008), g.Cards[2]);
        Assert.True(g.AnchoredBottom);
        Assert.Equal(g.Overlay.Bottom, g.Pill.Bottom);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void Layout_scales_with_dpi_and_stays_in_work_area(double scale)
    {
        var work = new PixelRect(0, 0, (int)(1920 * scale), (int)(1040 * scale));

        SelectorGeometry g = SelectorLayout.Compute(4, OverlaySize.Standard, PreviewStyle.Wallpaper, OverlayPosition.BottomCenter, work, scale, 0, 0);

        Assert.Equal((int)Math.Round(208 * scale), g.Cards[0].Width);
        Assert.True(g.Overlay.Left >= work.Left && g.Overlay.Right <= work.Right);
        Assert.True(g.Overlay.Bottom <= work.Bottom);
    }

    [Fact]
    public void Many_desktops_shrink_cards_to_fit()
    {
        SelectorGeometry g = SelectorLayout.Compute(12, OverlaySize.Standard, PreviewStyle.Wallpaper, OverlayPosition.BottomCenter, Work, 1.0, 0, 0);

        Assert.True(g.Overlay.Width <= Work.Width * 0.9 + 1);
        Assert.Equal(12, g.Cards.Count);
        Assert.True(g.Cards.Zip(g.Cards.Skip(1)).All(p => p.First.Right <= p.Second.Left));
    }

    [Fact]
    public void Top_center_anchors_to_top()
    {
        SelectorGeometry g = SelectorLayout.Compute(2, OverlaySize.Compact, PreviewStyle.Wallpaper, OverlayPosition.TopCenter, Work, 1.0, 0, 0);

        Assert.Equal(16, g.Overlay.Top);
        Assert.False(g.AnchoredBottom);
        Assert.Equal(g.Overlay.Top, g.Pill.Top);
        Assert.Equal(160, g.Cards[0].Width);
    }

    [Fact]
    public void Near_cursor_sits_above_cursor_and_flips_below_near_top()
    {
        SelectorGeometry above = SelectorLayout.Compute(2, OverlaySize.Standard, PreviewStyle.Wallpaper, OverlayPosition.NearCursor, Work, 1.0, 900, 700);
        Assert.Equal(700 - 48, above.Overlay.Bottom);

        SelectorGeometry below = SelectorLayout.Compute(2, OverlaySize.Standard, PreviewStyle.Wallpaper, OverlayPosition.NearCursor, Work, 1.0, 900, 100);
        Assert.Equal(100 + 48, below.Overlay.Top);
    }

    [Fact]
    public void Near_cursor_is_clamped_horizontally()
    {
        SelectorGeometry g = SelectorLayout.Compute(3, OverlaySize.Standard, PreviewStyle.Wallpaper, OverlayPosition.NearCursor, Work, 1.0, 5, 700);

        Assert.Equal(16, g.Overlay.Left);
    }

    [Fact]
    public void No_preview_cards_are_short()
    {
        SelectorGeometry g = SelectorLayout.Compute(2, OverlaySize.Standard, PreviewStyle.None, OverlayPosition.BottomCenter, Work, 1.0, 0, 0);

        Assert.Equal(72, g.Cards[0].Height);
    }

    [Fact]
    public void Hit_test_prefers_current_card_within_hysteresis()
    {
        PixelRect[] cards = [new(0, 0, 100, 100), new(108, 0, 208, 100)];

        Assert.Equal(0, CardHitTester.HitTest(cards, 104, 50, currentIndex: 0, hysteresisPx: 8));
        Assert.Null(CardHitTester.HitTest(cards, 104, 50, currentIndex: null, hysteresisPx: 8));
        Assert.Equal(1, CardHitTester.HitTest(cards, 150, 50, currentIndex: 0, hysteresisPx: 8));
    }

    [Fact]
    public void Dwell_fires_once_per_hover()
    {
        var tracker = new HoverDwellTracker(TimeSpan.FromMilliseconds(120));
        var t0 = DateTimeOffset.UnixEpoch;
        var delay = TimeSpan.FromMilliseconds(600);

        Assert.False(tracker.Update(1, t0, delay));
        Assert.True(tracker.Update(1, t0.AddMilliseconds(600), delay));
        Assert.False(tracker.Update(1, t0.AddMilliseconds(900), delay));
    }

    [Fact]
    public void Guard_enforces_in_flight_cooldown_and_leave_rule()
    {
        var guard = new SwitchGuard(TimeSpan.FromMilliseconds(400));
        var t0 = DateTimeOffset.UnixEpoch;

        guard.Begin();
        Assert.False(guard.CanSwitch(t0));
        guard.Complete(t0, succeeded: true);
        Assert.False(guard.CanSwitch(t0.AddSeconds(5))); // must leave cards first
        guard.CursorLeftCards();
        Assert.False(guard.CanSwitch(t0.AddMilliseconds(399)));
        Assert.True(guard.CanSwitch(t0.AddMilliseconds(400)));
    }

    [Fact]
    public void Failed_switch_does_not_start_cooldown()
    {
        var guard = new SwitchGuard(TimeSpan.FromMilliseconds(400));
        guard.Begin();
        guard.Complete(DateTimeOffset.UnixEpoch, succeeded: false);

        Assert.True(guard.CanSwitch(DateTimeOffset.UnixEpoch));
    }

    [Theory]
    [InlineData(10, 200)]
    [InlineData(5000, 1500)]
    [InlineData(650, 650)]
    public void Settings_clamp_hover_delay(int input, int expected)
    {
        var settings = new DragToSwitchSettings { HoverDelayMs = input };

        _ = settings.Normalize().ToList();

        Assert.Equal(expected, settings.HoverDelayMs);
    }

    [Fact]
    public void Settings_reset_undefined_enum_values()
    {
        var settings = new DragToSwitchSettings { OverlayPosition = (OverlayPosition)42 };

        Assert.Single(settings.Normalize());
        Assert.Equal(OverlayPosition.BottomCenter, settings.OverlayPosition);
    }
}
