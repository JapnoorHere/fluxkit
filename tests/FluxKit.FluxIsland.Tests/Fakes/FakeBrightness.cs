using FluxKit.Tools.FluxIsland.Abstractions;

namespace FluxKit.FluxIsland.Tests.Fakes;

internal sealed class FakeBrightness : IBrightnessSource
{
    public event EventHandler<int>? Changed;

    public void Set(int level) => Changed?.Invoke(this, level);

    public void Start()
    {
    }

    public void Dispose()
    {
    }
}
