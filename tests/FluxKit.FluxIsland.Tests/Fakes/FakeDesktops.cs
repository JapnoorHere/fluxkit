using FluxKit.Tools.FluxIsland.Abstractions;

namespace FluxKit.FluxIsland.Tests.Fakes;

internal sealed class FakeDesktops : IDesktopSource
{
    public event EventHandler<DesktopLanding>? Switched;

    public void Land(int number, int count, string name) => Switched?.Invoke(this, new DesktopLanding(number, count, name));

    public void Start()
    {
    }

    public void Dispose()
    {
    }
}
