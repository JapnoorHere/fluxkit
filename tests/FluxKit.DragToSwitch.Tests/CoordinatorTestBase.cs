using FluxKit.DragToSwitch.Tests.Fakes;
using FluxKit.Platform.Input;
using FluxKit.Platform.VirtualDesktop;
using FluxKit.Platform.Windows;
using FluxKit.Tools.DragToSwitch;
using FluxKit.Tools.DragToSwitch.Interaction;
using Microsoft.Extensions.Time.Testing;
using Serilog;

namespace FluxKit.DragToSwitch.Tests;

/// <summary>Shared fixture for the coordinator tests: fakes, a fake clock and input helpers.</summary>
public abstract class CoordinatorTestBase : IDisposable
{
    private protected static readonly (int X, int Y)[] CardCentre = [(740, 914), (960, 914), (1180, 914)];

    private protected readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero));
    private protected readonly FakeDesktops _desktops;
    private protected readonly FakeView _view = new();
    private protected readonly FakeProbe _probe = new();
    private protected readonly DragToSwitchSettings _settings = new();
    private protected DragInteractionCoordinator _sut;

    private protected CoordinatorTestBase()
    {
        _desktops = new FakeDesktops(3);
        _sut = Create();
    }

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
    }

    private protected DragInteractionCoordinator Create() =>
        new(_desktops, _view, _probe, _settings, _time, new LoggerConfiguration().CreateLogger());

    private protected void Recreate()
    {
        _sut.Dispose();
        _sut = Create();
    }

    private protected void Send(InputKind kind, int x = 0, int y = 0, int vk = 0) =>
        _sut.Process(new InputEvent(kind, x, y, vk, 0, false));

    private protected void Advance(int ms)
    {
        _time.Advance(TimeSpan.FromMilliseconds(ms));
        _sut.Tick();
    }

    private protected void StartDrag()
    {
        Send(InputKind.LeftDown, 100, 100);
        Send(InputKind.Move, 110, 100);
        Advance(120);
    }

    private protected void HoverCard(int index) => Send(InputKind.Move, CardCentre[index].X, CardCentre[index].Y);
}
