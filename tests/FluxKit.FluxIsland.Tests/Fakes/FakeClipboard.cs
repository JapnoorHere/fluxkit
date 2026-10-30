using FluxKit.Tools.FluxIsland.Abstractions;

namespace FluxKit.FluxIsland.Tests.Fakes;

internal sealed class FakeClipboard : IClipboardSource
{
    public event EventHandler<ClipboardCopied>? Copied;

    public bool Disposed { get; private set; }

    public void Copy(ClipboardKind kind, string? text = null) => Copied?.Invoke(this, new ClipboardCopied(kind, text));

    public void Start()
    {
    }

    public void Dispose() => Disposed = true;
}
