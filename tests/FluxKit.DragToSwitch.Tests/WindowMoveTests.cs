using FluxKit.DragToSwitch.Tests.Fakes;
using FluxKit.Platform.Input;
using FluxKit.Platform.VirtualDesktop;
using FluxKit.Platform.Windows;
using FluxKit.Tools.DragToSwitch;
using FluxKit.Tools.DragToSwitch.Interaction;
using Microsoft.Extensions.Time.Testing;
using Serilog;

namespace FluxKit.DragToSwitch.Tests;

/// <summary>Moving an app window to another desktop by dragging its title bar.</summary>
public sealed class WindowMoveTests : CoordinatorTestBase
{
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
}
