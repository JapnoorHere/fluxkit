namespace FluxKit.Platform.Watchers;

/// <summary>
/// One copy: what kind of thing it was and, only when the watcher was asked to read text, the text itself (cut to
/// <see cref="ClipboardWatcher.MaxTextLength"/> characters). Never logged.
/// </summary>
public readonly record struct ClipboardCopy(ClipboardContent Content, string? Text = null);
