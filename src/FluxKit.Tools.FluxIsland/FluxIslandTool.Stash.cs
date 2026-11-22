using FluxKit.Tools.FluxIsland.Activities;

namespace FluxKit.Tools.FluxIsland;

/// <summary>
/// Window stash (plan D): windows dragged onto the island are tucked away and listed on its Stash page until brought
/// back. The view does the window work (it knows where the island is); the tool keeps the list. Titles are shown on
/// screen only, never logged.
/// </summary>
public sealed partial class FluxIslandTool
{
    private readonly StashModel _stash = new();

    public void StashWindow(long handle, string title) =>
        Change(() =>
        {
            _stash.Add(new StashedWindow(handle, title, _time.GetUtcNow()));
            RaiseMoment(new IslandActivity(ActivityKind.Stashed) { Text = title, Value = _stash.Items.Count });
        });

    public void UnstashWindow(long handle) => Change(() => _stash.Remove(handle));

    public void PruneStash(Func<long, bool> exists) => Change(() => _stash.Prune(exists));
}
