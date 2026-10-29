using FluxKit.DragToSwitch.Tests.Fakes;
using FluxKit.Platform.Input;
using FluxKit.Platform.VirtualDesktop;
using FluxKit.Platform.Windows;
using FluxKit.Utilities.DragToSwitch;
using FluxKit.Utilities.DragToSwitch.Interaction;
using Microsoft.Extensions.Time.Testing;
using Serilog;

namespace FluxKit.DragToSwitch.Tests;

/// <summary>Hovering desktop cards: dwell timing, hysteresis, failed switches and cooldown.</summary>
public sealed class SwitchingTests : CoordinatorTestBase
{
    [Fact]
    public void Dwell_switches_exactly_at_hover_delay()
    {
        StartDrag();
        HoverCard(2);
        Advance(599);
        Assert.Empty(_desktops.SwitchRequests);
        Assert.Equal(CardVisualState.Dwelling, _view.CardStates[2].State);

        Advance(1);

        Assert.Equal([_desktops.Desktops[2].Id], _desktops.SwitchRequests);
        Assert.Equal(SessionPhase.Collapsed, _sut.Phase);
        Assert.True(_view.Collapsed);
        Assert.Equal(CardVisualState.Switched, _view.CardStates[2].State);
    }

    [Fact]
    public void Hovering_current_desktop_never_switches()
    {
        StartDrag();
        HoverCard(0);
        Advance(5000);

        Assert.Empty(_desktops.SwitchRequests);
    }

    [Fact]
    public void Brief_exit_within_grace_keeps_dwell()
    {
        StartDrag();
        HoverCard(1);
        Advance(400);
        Send(InputKind.Move, 960, 700); // off the selector
        Advance(100);
        HoverCard(1);
        Advance(100);

        Assert.Single(_desktops.SwitchRequests);
    }

    [Fact]
    public void Exit_longer_than_grace_resets_dwell()
    {
        StartDrag();
        HoverCard(1);
        Advance(400);
        Send(InputKind.Move, 960, 700);
        Advance(200);
        HoverCard(1);
        Advance(400);

        Assert.Empty(_desktops.SwitchRequests);
        Advance(200);
        Assert.Single(_desktops.SwitchRequests);
    }

    [Fact]
    public void Edge_wobble_inside_hysteresis_keeps_card()
    {
        StartDrag();
        HoverCard(1);
        Advance(300);
        Send(InputKind.Move, 960 + 104 + 4, 914); // 4px past the card edge, in the gap
        Advance(300);

        Assert.Single(_desktops.SwitchRequests);
    }

    [Fact]
    public void Failed_switch_shows_error_and_stays_active_without_success()
    {
        _desktops.NextResult = new SwitchResult(SwitchOutcome.Failed, TimeSpan.Zero, "Windows refused the switch.");
        StartDrag();
        HoverCard(1);
        Advance(600);

        Assert.Equal(SessionPhase.Active, _sut.Phase);
        Assert.False(_view.Collapsed);
        Assert.Equal((CardVisualState.Error, "Windows refused the switch."), _view.CardStates[1]);
        Assert.Equal(_desktops.Desktops[0].Id, _desktops.CurrentDesktopId);
    }

    [Fact]
    public void No_second_switch_until_cursor_leaves_cards_and_cooldown_passes()
    {
        _settings.CollapseAfterSwitch = false;
        Recreate();
        StartDrag();
        HoverCard(1);
        Advance(600);
        Assert.Single(_desktops.SwitchRequests);
        Assert.Equal(SessionPhase.WaitingForRelease, _sut.Phase);

        HoverCard(2);
        Advance(2000);

        Assert.Single(_desktops.SwitchRequests);
    }

    [Fact]
    public void Pill_hover_expands_and_allows_another_switch()
    {
        StartDrag();
        HoverCard(1);
        Advance(600);
        Assert.True(_view.Collapsed);

        Send(InputKind.Move, 960, 1000); // inside the pill (bottom-centre)
        Advance(16);
        Assert.False(_view.Collapsed);
        Assert.Equal(SessionPhase.Active, _sut.Phase);

        Send(InputKind.Move, 960, 700); // leave the cards to re-arm the guard
        Advance(500);
        HoverCard(2);
        Advance(600);

        Assert.Equal(2, _desktops.SwitchRequests.Count);
        Assert.Equal(_desktops.Desktops[2].Id, _desktops.SwitchRequests[1]);
    }

    [Fact]
    public void Cursor_over_selector_shows_drop_hint()
    {
        StartDrag();
        Send(InputKind.Move, 700, 850);
        Assert.True(_view.OverSelector);

        Send(InputKind.Move, 700, 500);
        Assert.False(_view.OverSelector);
    }
}
