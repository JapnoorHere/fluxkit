namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>Raised when something is copied. Only the kind of content is reported; the content is never read.</summary>
public interface IClipboardSource : IIslandSource
{
    event EventHandler<ClipboardKind>? Copied;
}

public enum ClipboardKind
{
    Other,
    Text,
    Image,
    Files,
}
