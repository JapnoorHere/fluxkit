using FluxKit.DragToSwitch.Tests.Fakes;
using FluxKit.Platform.Input;
using FluxKit.Platform.VirtualDesktop;
using FluxKit.Platform.Windows;
using FluxKit.Utilities.DragToSwitch;
using FluxKit.Utilities.DragToSwitch.Interaction;
using Microsoft.Extensions.Time.Testing;
using Serilog;

namespace FluxKit.DragToSwitch.Tests;

/// <summary>How sessions end: release, desktop changes, timeouts and aborts.</summary>
public sealed class SessionLifecycleTests : CoordinatorTestBase
{
    [Fact]
    public void Release_ends_session_as_released_without_claiming_delivery()
    {
        SessionSummary? ended = null;
        _sut.SessionEnded += (_, s) => ended = s;
        StartDrag();
        HoverCard(1);
        Advance(600);

        Send(InputKind.LeftUp, 500, 500);

        Assert.Equal(SessionPhase.Idle, _sut.Phase);
        Assert.Equal(1, _view.HideCount);
        Assert.NotNull(ended);
        Assert.Equal(SessionEnd.Released, ended.End);
        Assert.Equal(1, ended.FromDesktop);
        Assert.Equal(2, ended.ToDesktop);
    }

    [Fact]
    public void Removing_hovered_desktop_mid_dwell_cancels_switch()
    {
        StartDrag();
        HoverCard(2);
        Advance(300);

        _desktops.Remove(2);
        Advance(400);

        Assert.Empty(_desktops.SwitchRequests);
        Assert.Equal(2, _view.Cards.Count);
    }

    [Fact]
    public void Dropping_below_two_desktops_aborts_session()
    {
        StartDrag();

        _desktops.Remove(2);
        _desktops.Remove(1);

        Assert.Equal(SessionEnd.Aborted, _sut.LastSession!.End);
        Assert.Equal(SessionPhase.WaitingForRelease, _sut.Phase);
    }

    [Fact]
    public void Release_during_inflight_switch_ignores_late_completion()
    {
        _desktops.Pending = new TaskCompletionSource<SwitchResult>();
        StartDrag();
        HoverCard(1);
        Advance(600);
        Assert.Equal(SessionPhase.Switching, _sut.Phase);

        Send(InputKind.LeftUp);
        _desktops.Pending.SetResult(new SwitchResult(SwitchOutcome.Success, TimeSpan.FromMilliseconds(5)));

        Assert.Equal(SessionPhase.Idle, _sut.Phase);
        Assert.False(_view.Collapsed);
    }

    [Fact]
    public void Session_times_out()
    {
        StartDrag();
        Advance(120_001);

        Assert.Equal(SessionEnd.Aborted, _sut.LastSession!.End);
    }

    [Fact]
    public void Abort_hides_selector_and_returns_to_idle()
    {
        StartDrag();

        _sut.Abort("disabled");

        Assert.Equal(SessionPhase.Idle, _sut.Phase);
        Assert.Equal(1, _view.HideCount);
    }

    [Fact]
    public void Repeated_sessions_are_independent()
    {
        for (int i = 0; i < 3; i++)
        {
            _desktops.SetCurrent(_desktops.Desktops[0].Id);
            StartDrag();
            HoverCard(1);
            Advance(600);
            Send(InputKind.LeftUp);
            Advance(500);
        }

        Assert.Equal(3, _view.ShowCount);
        Assert.Equal(3, _desktops.SwitchRequests.Count);
        Assert.Equal(SessionPhase.Idle, _sut.Phase);
    }
}
