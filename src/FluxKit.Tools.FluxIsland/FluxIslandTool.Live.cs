using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;

namespace FluxKit.Tools.FluxIsland;

/// <summary>
/// Live Activities (plan B, ADR-016): scripts, terminals, builds and AI agents send short updates through a local pipe;
/// the newest one still running lives on the pill with its progress, and one that finishes takes over for a moment,
/// with a check or a shake. Display only: an update can never make FluxKit run anything. Logs record ids and states,
/// never text.
/// </summary>
public sealed partial class FluxIslandTool
{
    private readonly LiveActivityModel _live = new();

    /// <summary>Starts the pipe (kept with the other sources, so it stops with them).</summary>
    private void StartLive() =>
        _ = Start(_options.LiveActivities ? _factories.Live : null, s => s.LineReceived += OnLiveLine);

    private void OnLiveLine(object? sender, string line)
    {
        if (!_options.LiveActivities || !LiveProtocol.TryParse(line, out LiveUpdate? update) || update is null)
        {
            _log.Debug("Ignored a Live Activity line that wasn't valid");
            return;
        }

        _log.Debug("Live Activity {Id}: {State}", update.Id, update.State);
        Change(() =>
        {
            if (_live.Apply(update, _time.GetUtcNow()) is { } finished)
            {
                RaiseMoment(new IslandActivity(ActivityKind.LiveDone)
                {
                    Text = finished.Title,
                    Detail = finished.Detail,
                    Source = finished.Glyph,
                    Path = finished.Id,
                    Flag = update.State == LiveState.Failed,
                });
            }
        });
    }

    /// <summary>Puts the most recently updated Live Activity on the pill (the others wait their turn), dropping expired ones.</summary>
    private void SyncLive()
    {
        _live.Expire(_time.GetUtcNow());
        LiveActivity? newest = _options.LiveActivities && _live.Items is [var first, ..] ? first : null;
        _arbiter.SetLasting(ActivityKind.Live, newest is null ? null : new IslandActivity(ActivityKind.Live)
        {
            Text = newest.Title,
            Detail = newest.Detail,
            Value = newest.Progress ?? -1,
            Total = _live.Items.Count,
            Source = newest.Glyph,
            Path = newest.Id,
        });
    }
}
