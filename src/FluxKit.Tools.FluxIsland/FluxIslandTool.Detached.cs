using FluxKit.Tools.FluxIsland.Activities;

namespace FluxKit.Tools.FluxIsland;

/// <summary>
/// Tear-off droplets (plan A): music, a timer or a stopwatch dragged off the island lives in its own floating droplet
/// until it is docked back or ends. The pill steps aside for it; the droplet's place is kept in the settings, so it
/// comes back where it was after a restart.
/// </summary>
public sealed partial class FluxIslandTool
{
    private readonly DetachedActivities _detached = new();

    public void DetachActivity(ActivityKind kind, int x, int y) =>
        ChangeDroplets(() => _detached.Detach(kind, x, y));

    public void MoveDroplet(ActivityKind kind, int x, int y) =>
        ChangeDroplets(() => _detached.Move(kind, x, y));

    public void DockActivity(ActivityKind kind) =>
        ChangeDroplets(() => _detached.Dock(kind));

    /// <summary>Takes detached activities off the pill, and docks any whose activity has ended.</summary>
    private void SyncDetached()
    {
        bool changed = false;
        foreach (DropletPlace place in _detached.Places)
        {
            bool active = place.Kind switch
            {
                ActivityKind.Media => _options.ShowMedia && _media?.Current is not null,
                ActivityKind.Timer => _timer.Snapshot.IsActive,
                ActivityKind.Stopwatch => _stopwatch.Snapshot.IsActive,
                _ => false,
            };
            if (active)
            {
                _arbiter.SetLasting(place.Kind, null);
            }
            else
            {
                changed |= _detached.Dock(place.Kind);
            }
        }

        if (changed)
        {
            SaveDroplets();
        }
    }

    private void ChangeDroplets(Func<bool> change)
    {
        bool changed = false;
        Change(() => changed = change());
        if (changed)
        {
            SaveDroplets();
        }
    }

    private void SaveDroplets()
    {
        List<DropletPlace> places;
        lock (_sync)
        {
            places = [.. _detached.Places];
        }

        _ = _settings.UpdateAsync<FluxIslandSettings>(FluxIslandSettings.Section, s =>
        {
            s.Droplets = places;
            return s;
        });
    }
}
