using FluxKit.Platform.Input;
using FluxKit.Platform.VirtualDesktop;
using FluxKit.Platform.Windows;
using FluxKit.Utilities.DragToSwitch;
using FluxKit.Utilities.DragToSwitch.Interaction;
using Microsoft.Extensions.Time.Testing;
using Serilog;

namespace FluxKit.DragToSwitch.Tests;

/// <summary>
/// Lifecycle tests for the coordinator. With 3 desktops, standard cards, bottom-centre on a 1920x1040
/// work area at 100% scale, card centres are (740,914), (960,914) and (1180,914); the overlay spans
/// x 620..1300, y 764..1024.
/// </summary>
public sealed class CoordinatorTests : IDisposable
{
    private static readonly (int X, int Y)[] CardCentre = [(740, 914), (960, 914), (1180, 914)];

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeDesktops _desktops;
    private readonly FakeView _view = new();
    private readonly FakeProbe _probe = new();
    private readonly DragToSwitchSettings _settings = new();
    private DragInteractionCoordinator _sut;

    public CoordinatorTests()
    {
        _desktops = new FakeDesktops(3);
        _sut = Create();
    }

    public void Dispose() => _sut.Dispose();

    private DragInteractionCoordinator Create() =>
        new(_desktops, _view, _probe, _settings, _time, new LoggerConfiguration().CreateLogger());

    private void Recreate()
    {
        _sut.Dispose();
        _sut = Create();
    }

    private void Send(InputKind kind, int x = 0, int y = 0, int vk = 0) =>
        _sut.Process(new InputEvent(kind, x, y, vk, 0, false));

    private void Advance(int ms)
    {
        _time.Advance(TimeSpan.FromMilliseconds(ms));
        _sut.Tick();
    }

    private void StartDrag()
    {
        Send(InputKind.LeftDown, 100, 100);
        Send(InputKind.Move, 110, 100);
        Advance(120);
    }

    private void HoverCard(int index) => Send(InputKind.Move, CardCentre[index].X, CardCentre[index].Y);

    [Fact]
    public void Explorer_file_drag_shows_selector_after_debounce()
    {
        Send(InputKind.LeftDown, 100, 100);
        Send(InputKind.Move, 110, 100);
        Advance(119);
        Assert.Equal(0, _view.ShowCount);

        Advance(1);

        Assert.Equal(SessionPhase.Active, _sut.Phase);
        Assert.Equal(1, _view.ShowCount);
        Assert.Equal(3, _view.Cards.Count);
    }

    [Theory]
    [InlineData(DragSources.ExplorerAndDesktop, "Shell_TrayWnd")]
    [InlineData(DragSources.ExplorerAndDesktop, "Shell_SecondaryTrayWnd")]
    [InlineData(DragSources.AnyApp, "Shell_TrayWnd")]
    [InlineData(DragSources.AnyApp, "TopLevelWindowForOverflowXamlIsland")]
    public void Drag_from_taskbar_or_tray_never_shows_selector(DragSources sources, string className)
    {
        _settings.Sources = sources;
        Recreate();
        _probe.ClassName = className;
        StartDrag();
        Advance(500);

        Assert.Equal(0, _view.ShowCount);
    }

    [Fact]
    public void Drag_from_desktop_icons_still_shows_selector()
    {
        _probe.ClassName = "Progman";
        StartDrag();

        Assert.Equal(1, _view.ShowCount);
    }

    [Fact]
    public void Drag_from_non_explorer_source_is_ignored_by_default()
    {
        _probe.ProcessName = "notepad";
        StartDrag();

        Assert.Equal(0, _view.ShowCount);
    }

    [Fact]
    public void Any_app_mode_accepts_other_sources()
    {
        _probe.ProcessName = "chrome";
        _settings.Sources = DragSources.AnyApp;
        Recreate();

        StartDrag();
        Advance(16); // the payload check confirms files on the next tick

        Assert.Equal(1, _view.ShowCount);
    }

    [Theory]
    [InlineData(DragSources.AnyApp, "chrome")]
    [InlineData(DragSources.ExplorerAndDesktop, "explorer")]
    public void Text_selection_or_scrollbar_drag_never_shows_selector(DragSources sources, string process)
    {
        _settings.Sources = sources;
        Recreate();
        _probe.ProcessName = process;
        _probe.OleDrag = false; // the app keeps mouse capture; no OLE drag ever starts
        StartDrag();
        Advance(500);
        Assert.Equal(SessionPhase.Suspected, _sut.Phase);

        Advance(500);

        Assert.Equal(0, _view.ShowCount);
        Assert.Equal(SessionPhase.WaitingForRelease, _sut.Phase);
    }

    [Fact]
    public void Selector_waits_for_an_ole_drag_that_starts_late()
    {
        _probe.OleDrag = false;
        StartDrag();
        Advance(200);
        Assert.Equal(0, _view.ShowCount);

        _probe.OleDrag = true;
        Advance(16);

        Assert.Equal(1, _view.ShowCount);
        Assert.Equal(DragKind.File, _view.Kind);
    }

    [Theory]
    [InlineData(DragPayload.Files, 1)]
    [InlineData(DragPayload.Other, 0)]
    public void Any_app_drag_shows_selector_only_when_it_carries_files(DragPayload payload, int expectedShows)
    {
        _settings.Sources = DragSources.AnyApp;
        Recreate();
        _probe.ProcessName = "chrome";
        _probe.Payload = payload;
        StartDrag();
        Advance(300);

        Assert.Equal(1, _probe.PayloadChecksStarted);
        Assert.False(_probe.PayloadCheckRunning);
        Assert.Equal(expectedShows, _view.ShowCount);
    }

    [Fact]
    public void Any_app_drag_is_ignored_when_the_payload_check_never_sees_it()
    {
        _settings.Sources = DragSources.AnyApp;
        Recreate();
        _probe.ProcessName = "chrome";
        _probe.Payload = DragPayload.Unknown;
        StartDrag();
        Advance(500);
        Assert.True(_probe.PayloadCheckRunning);

        Advance(400);

        Assert.False(_probe.PayloadCheckRunning);
        Assert.Equal(0, _view.ShowCount);
        Assert.Equal(SessionPhase.WaitingForRelease, _sut.Phase);
    }

    [Fact]
    public void Explorer_file_drag_needs_no_payload_check()
    {
        StartDrag();
        Advance(200);

        Assert.Equal(0, _probe.PayloadChecksStarted);
        Assert.Equal(1, _view.ShowCount);
    }

    [Fact]
    public void Release_during_payload_check_hides_the_check()
    {
        _settings.Sources = DragSources.AnyApp;
        Recreate();
        _probe.ProcessName = "chrome";
        _probe.Payload = DragPayload.Unknown;
        StartDrag();
        Advance(200);
        Assert.True(_probe.PayloadCheckRunning);

        Send(InputKind.LeftUp, 140, 100);

        Assert.False(_probe.PayloadCheckRunning);
        Assert.Equal(0, _view.ShowCount);
    }

    [Fact]
    public void Release_before_threshold_never_shows_selector()
    {
        Send(InputKind.LeftDown, 100, 100);
        Send(InputKind.Move, 102, 101);
        Send(InputKind.LeftUp, 102, 101);
        Advance(500);

        Assert.Equal(0, _view.ShowCount);
        Assert.Equal(SessionPhase.Idle, _sut.Phase);
    }

    [Fact]
    public void Resize_or_menu_is_not_treated_as_a_drag()
    {
        _probe.MoveSize = true; // move-size loop without a window being moved = resize or menu tracking
        StartDrag();

        Assert.Equal(0, _view.ShowCount);
        Assert.Equal(SessionPhase.WaitingForRelease, _sut.Phase);
    }

    [Fact]
    public void Title_bar_drag_shows_selector_for_windows()
    {
        _probe.ProcessName = "chrome"; // window moves work for any app, not just Explorer
        _probe.MovingWindow = 4242;
        StartDrag();

        Assert.Equal(1, _view.ShowCount);
        Assert.Equal(DragKind.Window, _view.Kind);
    }

    [Fact]
    public void Window_travels_with_the_switch()
    {
        _probe.MovingWindow = 4242;
        Guid target = _desktops.Desktops[2].Id;
        StartDrag();
        HoverCard(2);
        Advance(600);

        Assert.Equal([(4242, target)], _desktops.WindowMoves);
        Assert.Equal(target, _desktops.CurrentDesktopId);
        Assert.True(_view.Collapsed);
    }

    [Fact]
    public void Window_move_failure_shows_error_and_does_not_switch()
    {
        _probe.MovingWindow = 4242;
        _desktops.NextMoveError = "This window can't be moved to another desktop.";
        Guid origin = _desktops.CurrentDesktopId;
        StartDrag();
        HoverCard(2);
        Advance(600);

        Assert.Equal(origin, _desktops.CurrentDesktopId);
        Assert.Empty(_desktops.SwitchRequests);
        Assert.Equal((CardVisualState.Error, "This window can't be moved to another desktop."), _view.CardStates[2]);
        Assert.Equal(SessionPhase.Active, _sut.Phase);
    }

    [Fact]
    public void Escape_after_window_switch_puts_the_window_back()
    {
        _probe.MovingWindow = 4242;
        Guid origin = _desktops.CurrentDesktopId;
        StartDrag();
        HoverCard(2);
        Advance(600);

        Send(InputKind.KeyDown, vk: 0x1B);

        Assert.Equal((4242, origin), _desktops.WindowMoves[^1]);
        Assert.NotEqual(origin, _desktops.CurrentDesktopId); // user stays unless "return to origin" is on
    }

    [Fact]
    public void Escape_after_window_switch_returns_window_and_user_when_enabled()
    {
        _settings.ReturnToOriginOnCancel = true;
        Recreate();
        _probe.MovingWindow = 4242;
        Guid origin = _desktops.CurrentDesktopId;
        StartDrag();
        HoverCard(2);
        Advance(600);

        Send(InputKind.KeyDown, vk: 0x1B);

        Assert.Equal((4242, origin), _desktops.WindowMoves[^1]);
        Assert.Equal(origin, _desktops.CurrentDesktopId);
    }

    [Fact]
    public void Window_drags_are_ignored_when_disabled()
    {
        _settings.MoveWindowsEnabled = false;
        Recreate();
        _probe.MovingWindow = 4242;
        StartDrag();

        Assert.Equal(0, _view.ShowCount);
    }

    [Fact]
    public void Window_drags_are_ignored_without_shell_integration()
    {
        _desktops.CanMoveWindows = false; // keyboard fallback can't move other apps' windows
        _probe.MovingWindow = 4242;
        StartDrag();

        Assert.Equal(0, _view.ShowCount);
    }

    [Fact]
    public void Non_explorer_click_without_window_move_stays_ignored()
    {
        _probe.ProcessName = "notepad"; // e.g. text selection in Notepad
        StartDrag();

        Assert.Equal(0, _view.ShowCount);
        Assert.Empty(_desktops.WindowMoves);
    }

    [Fact]
    public void Full_screen_app_suppresses_selector()
    {
        _probe.FullScreen = true;
        StartDrag();

        Assert.Equal(0, _view.ShowCount);
    }

    [Fact]
    public void Single_desktop_does_not_show_selector()
    {
        _desktops.Remove(2);
        _desktops.Remove(1);
        StartDrag();

        Assert.Equal(0, _view.ShowCount);
    }

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
    public void Escape_cancels_and_waits_for_release()
    {
        StartDrag();
        Send(InputKind.KeyDown, vk: 0x1B);

        Assert.Equal(SessionPhase.WaitingForRelease, _sut.Phase);
        Assert.Equal(SessionEnd.Cancelled, _sut.LastSession!.End);
        Assert.Empty(_desktops.SwitchRequests);
        Send(InputKind.LeftUp);
        Assert.Equal(SessionPhase.Idle, _sut.Phase);
    }

    [Fact]
    public void Escape_after_switch_returns_to_origin_when_enabled()
    {
        _settings.ReturnToOriginOnCancel = true;
        Recreate();
        Guid origin = _desktops.CurrentDesktopId;
        StartDrag();
        HoverCard(2);
        Advance(600);

        Send(InputKind.KeyDown, vk: 0x1B);

        Assert.Equal(origin, _desktops.SwitchRequests[^1]);
        Assert.Equal(origin, _desktops.CurrentDesktopId);
    }

    [Fact]
    public void Number_key_switches_and_is_swallowed_only_while_expanded()
    {
        var digit3 = new InputEvent(InputKind.KeyDown, 0, 0, 0x33, 0, false);
        Assert.False(_sut.ShouldSwallowKey(digit3));

        StartDrag();
        Assert.True(_sut.ShouldSwallowKey(digit3));

        _sut.Process(digit3);

        Assert.Equal([_desktops.Desktops[2].Id], _desktops.SwitchRequests);
        Assert.False(_sut.ShouldSwallowKey(digit3)); // collapsed now
    }

    [Fact]
    public void Number_keys_disabled_by_setting()
    {
        _settings.NumberKeysEnabled = false;
        Recreate();
        StartDrag();
        var digit2 = new InputEvent(InputKind.KeyDown, 0, 0, 0x32, 0, false);

        Assert.False(_sut.ShouldSwallowKey(digit2));
        _sut.Process(digit2);
        Assert.Empty(_desktops.SwitchRequests);
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
    public void Unavailable_backend_never_shows_selector()
    {
        _desktops.Backend = VirtualDesktopBackend.Unavailable;
        StartDrag();

        Assert.Equal(0, _view.ShowCount);
    }

    [Fact]
    public void Edge_summon_waits_for_cursor_at_edge()
    {
        _settings.Trigger = SelectorTrigger.EdgeSummon;
        Recreate();
        StartDrag();
        Assert.Equal(0, _view.ShowCount);

        Send(InputKind.Move, 900, 1000);

        Assert.Equal(1, _view.ShowCount);
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
