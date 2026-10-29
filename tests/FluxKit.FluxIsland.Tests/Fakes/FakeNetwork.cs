using FluxKit.Tools.FluxIsland.Abstractions;

namespace FluxKit.FluxIsland.Tests.Fakes;

internal sealed class FakeNetwork : INetworkSource
{
    public bool IsOnline { get; private set; } = true;

    public event EventHandler? Changed;

    public void Set(bool online)
    {
        IsOnline = online;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Start()
    {
    }

    public void Dispose()
    {
    }
}
