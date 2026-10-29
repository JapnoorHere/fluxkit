using FluxKit.Platform.Watchers;
using FluxKit.Tools.FluxIsland.Abstractions;
using Serilog;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary><see cref="IClipboardSource"/> backed by the Platform <see cref="ClipboardWatcher"/>.</summary>
internal sealed class ClipboardSource : IClipboardSource
{
    private readonly ClipboardWatcher _watcher;

    public ClipboardSource(ILogger log)
    {
        _watcher = new ClipboardWatcher(log);
        _watcher.Copied += (_, content) => Copied?.Invoke(this, content switch
        {
            ClipboardContent.Text => ClipboardKind.Text,
            ClipboardContent.Image => ClipboardKind.Image,
            ClipboardContent.Files => ClipboardKind.Files,
            _ => ClipboardKind.Other,
        });
    }

    public event EventHandler<ClipboardKind>? Copied;

    public void Start() => _watcher.Start();

    public void Dispose() => _watcher.Dispose();
}
