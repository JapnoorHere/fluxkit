using FluxKit.Tools.FluxIsland.Abstractions;

namespace FluxKit.FluxIsland.Tests.Fakes;

internal sealed class FakeFullScreen : IFullScreenSource
{
    public bool IsFullScreen { get; private set; }

    public event EventHandler? Changed;

    public void Set(bool fullScreen)
    {
        IsFullScreen = fullScreen;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Start()
    {
    }

    public void Dispose()
    {
    }
}
