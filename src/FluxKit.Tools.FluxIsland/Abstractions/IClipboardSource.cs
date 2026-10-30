namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>
/// Raised when something is copied. Only the kind of content is reported, unless smart copy is on: then the text of a
/// text copy comes too, to be looked at once and never kept or logged. Private copies (password managers) never raise it.
/// </summary>
public interface IClipboardSource : IIslandSource
{
    event EventHandler<ClipboardCopied>? Copied;
}

public enum ClipboardKind
{
    Other,
    Text,
    Image,
    Files,
}

/// <summary>One copy: its kind and, with smart copy on, its text.</summary>
public readonly record struct ClipboardCopied(ClipboardKind Kind, string? Text = null);
