using FluxKit.DragToSwitch.Tests.Fakes;
using FluxKit.Platform.Input;
using FluxKit.Platform.VirtualDesktop;
using FluxKit.Platform.Windows;
using FluxKit.Tools.DragToSwitch;
using FluxKit.Tools.DragToSwitch.Interaction;
using Microsoft.Extensions.Time.Testing;
using Serilog;

namespace FluxKit.DragToSwitch.Tests;

/// <summary>Esc and number keys during a drag.</summary>
public sealed class KeyboardTests : CoordinatorTestBase
{
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
}
