using FluxKit.Tools.FluxIsland.Abstractions;

namespace FluxKit.FluxIsland.Tests.Fakes;

internal sealed class FakeClipboard : IClipboardSource
{
    public event EventHandler<ClipboardKind>? Copied;

    public bool Disposed { get; private set; }

    public void Copy(ClipboardKind kind) => Copied?.Invoke(this, kind);

    public void Start()
    {
    }

    public void Dispose() => Disposed = true;
}
