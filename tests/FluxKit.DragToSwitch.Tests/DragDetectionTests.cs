using FluxKit.DragToSwitch.Tests.Fakes;
using FluxKit.Platform.Input;
using FluxKit.Platform.VirtualDesktop;
using FluxKit.Platform.Windows;
using FluxKit.Utilities.DragToSwitch;
using FluxKit.Utilities.DragToSwitch.Interaction;
using Microsoft.Extensions.Time.Testing;
using Serilog;

namespace FluxKit.DragToSwitch.Tests;

/// <summary>When the coordinator shows the desktop picker for a drag, and when it stays out of the way.</summary>
public sealed class DragDetectionTests : CoordinatorTestBase
{
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
}
