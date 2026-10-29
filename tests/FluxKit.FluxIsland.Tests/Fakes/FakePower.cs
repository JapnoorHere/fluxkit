using FluxKit.Tools.FluxIsland.Abstractions;

namespace FluxKit.FluxIsland.Tests.Fakes;

internal sealed class FakePower(PowerSnapshot initial) : IPowerSource
{
    public PowerSnapshot Current { get; private set; } = initial;

    public event EventHandler? Changed;

    public void Set(PowerSnapshot power)
    {
        Current = power;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Start()
    {
    }

    public void Dispose()
    {
    }
}
