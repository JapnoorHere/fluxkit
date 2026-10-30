using FluxKit.Platform.VirtualDesktop;
using FluxKit.Tools.FluxIsland.Abstractions;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary>
/// <see cref="IDesktopSource"/> over FluxKit's shared <see cref="IVirtualDesktopService"/>. That service raises one
/// event for every kind of desktop change; only a change of the current desktop counts as a switch.
/// </summary>
internal sealed class DesktopSource(IVirtualDesktopService desktops) : IDesktopSource
{
    private readonly Lock _sync = new();
    private Guid _current;

    public event EventHandler<DesktopLanding>? Switched;

    public void Start()
    {
        _current = desktops.CurrentDesktopId;
        desktops.DesktopsChanged += OnDesktopsChanged;
    }

    public void Dispose() => desktops.DesktopsChanged -= OnDesktopsChanged;

    private void OnDesktopsChanged(object? sender, EventArgs e)
    {
        IReadOnlyList<VirtualDesktopInfo> all = desktops.Desktops;
        Guid now = desktops.CurrentDesktopId;
        lock (_sync)
        {
            if (now == _current || now == Guid.Empty)
            {
                return;
            }

            _current = now;
        }

        if (all.FirstOrDefault(d => d.Id == now) is { } landed && all.Count > 1)
        {
            Switched?.Invoke(this, new DesktopLanding(landed.Index + 1, all.Count, landed.Name));
        }
    }
}
