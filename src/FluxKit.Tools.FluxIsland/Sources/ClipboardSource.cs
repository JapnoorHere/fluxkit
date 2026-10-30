using FluxKit.Platform.Watchers;
using FluxKit.Tools.FluxIsland.Abstractions;
using Serilog;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary><see cref="IClipboardSource"/> backed by the Platform <see cref="ClipboardWatcher"/>.</summary>
internal sealed class ClipboardSource : IClipboardSource
{
    private readonly ClipboardWatcher _watcher;

    public ClipboardSource(ILogger log, bool readText)
    {
        _watcher = new ClipboardWatcher(log, readText);
        _watcher.Copied += (_, copy) => Copied?.Invoke(this, new ClipboardCopied(
            copy.Content switch
            {
                ClipboardContent.Text => ClipboardKind.Text,
                ClipboardContent.Image => ClipboardKind.Image,
                ClipboardContent.Files => ClipboardKind.Files,
                _ => ClipboardKind.Other,
            },
            copy.Text));
    }

    public event EventHandler<ClipboardCopied>? Copied;

    public void Start() => _watcher.Start();

    public void Dispose() => _watcher.Dispose();
}
