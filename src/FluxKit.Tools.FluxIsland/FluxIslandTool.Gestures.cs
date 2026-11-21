using FluxKit.Tools.FluxIsland.Activities;

namespace FluxKit.Tools.FluxIsland;

/// <summary>Gestures that change what the island shows: a moment thrown sideways is snoozed and comes back later.</summary>
public sealed partial class FluxIslandTool
{
    private readonly SnoozedMoments _snoozes = new();

    public void SnoozeAlert() =>
        Change(() =>
        {
            if (_arbiter.Primary is { IsAlert: true } alert)
            {
                _snoozes.Add(alert, _time.GetUtcNow() + SnoozedMoments.Delay);
                _arbiter.Dismiss();
            }
        });

    /// <summary>Brings back every snoozed moment that is due (called when the island wakes).</summary>
    private void ReturnSnoozed()
    {
        foreach (IslandActivity moment in _snoozes.TakeDue(_time.GetUtcNow()))
        {
            RaiseMoment(moment);
        }
    }
}
