using FluxKit.Platform.Watchers;
using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using Serilog;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary><see cref="ILockKeysSource"/> backed by the Platform <see cref="LockKeysWatcher"/>.</summary>
internal sealed class LockKeysSource : ILockKeysSource
{
    private readonly LockKeysWatcher _watcher;

    public LockKeysSource(ILogger log)
    {
        _watcher = new LockKeysWatcher(log);
        _watcher.Toggled += (_, e) => Changed?.Invoke(this, new LockKeyChange(e.IsCapsLock, e.On));
    }

    public event EventHandler<LockKeyChange>? Changed;

    public void Start() => _watcher.Start();

    public void Dispose() => _watcher.Dispose();
}

/// <summary><see cref="IHotkeySource"/> backed by the Platform <see cref="HotkeyWatcher"/>.</summary>
internal sealed class HotkeySource : IHotkeySource
{
    private readonly HotkeyWatcher _watcher;

    public HotkeySource(HotkeyGesture gesture, ILogger log)
    {
        // HotkeyModifiers uses Windows' own MOD_* values.
        _watcher = new HotkeyWatcher((uint)gesture.Modifiers, (uint)gesture.Key, log);
        _watcher.Pressed += (_, _) => Pressed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Pressed;

    public void Start() => _watcher.Start();

    public void Dispose() => _watcher.Dispose();
}
