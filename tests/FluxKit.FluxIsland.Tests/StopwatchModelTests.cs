using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.Extensions.Time.Testing;

namespace FluxKit.FluxIsland.Tests;

public sealed class StopwatchModelTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly StopwatchModel _stopwatch;

    public StopwatchModelTests() => _stopwatch = new StopwatchModel(_time);

    [Fact]
    public void Counts_up_while_running_and_holds_while_paused()
    {
        _stopwatch.Start();
        _time.Advance(TimeSpan.FromSeconds(10));
        _stopwatch.PauseOrResume();
        _time.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(TimeSpan.FromSeconds(10), _stopwatch.Snapshot.ElapsedAt(_time.GetUtcNow()));

        _stopwatch.PauseOrResume();
        _time.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal(TimeSpan.FromSeconds(15), _stopwatch.Snapshot.ElapsedAt(_time.GetUtcNow()));
    }

    [Fact]
    public void Laps_keep_each_laps_own_length_newest_first()
    {
        _stopwatch.Start();
        _time.Advance(TimeSpan.FromSeconds(30));
        _stopwatch.Lap();
        _time.Advance(TimeSpan.FromSeconds(12));
        _stopwatch.Lap();

        Assert.Equal([TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(30)], _stopwatch.Snapshot.Laps);
    }

    [Fact]
    public void Reset_goes_back_to_idle()
    {
        _stopwatch.Start();
        _time.Advance(TimeSpan.FromSeconds(3));
        _stopwatch.Reset();

        Assert.False(_stopwatch.Snapshot.IsActive);
        Assert.Equal(TimeSpan.Zero, _stopwatch.Snapshot.ElapsedAt(_time.GetUtcNow()));
    }
}
