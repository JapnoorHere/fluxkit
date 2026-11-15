using FluxKit.Tools.FluxIsland.Abstractions;

namespace FluxKit.FluxIsland.Tests.Fakes;

internal sealed class FakePrivacy : IPrivacySource
{
    public PrivacySnapshot Current { get; private set; }

    public event EventHandler? Changed;

    public void Set(bool microphone, bool camera = false, bool call = false)
    {
        Current = new PrivacySnapshot(microphone, camera, call);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Start()
    {
    }

    public void Dispose()
    {
    }
}
