using FluxKit.Platform.Audio;
using Microsoft.UI.Dispatching;

namespace FluxKit.App.Modules.FluxIsland.Design;

/// <summary>
/// Samples the speakers' peak level about 30 times a second, but only while <see cref="SetRunning"/> says something
/// on screen shows it (the music bars while music plays). The level rises at once and falls away gently, the way a
/// real meter's needle does. When stopped, nothing runs at all.
/// </summary>
internal sealed class LevelMeter : IDisposable
{
    private const float Fall = 0.82f;

    private readonly PeakMeter _peak = new();
    private readonly DispatcherQueueTimer _timer;
    private float _level;

    public LevelMeter(DispatcherQueue queue)
    {
        _timer = queue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(33);
        _timer.Tick += (_, _) => Sample();
    }

    /// <summary>The smoothed level from 0 to 1, raised on the UI thread after each sample.</summary>
    public event EventHandler<float>? Changed;

    public void SetRunning(bool running)
    {
        if (running && !_timer.IsRunning)
        {
            _timer.Start();
        }
        else if (!running && _timer.IsRunning)
        {
            _timer.Stop();
            _level = 0;
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _peak.Dispose();
    }

    private void Sample()
    {
        // Peaks are small numbers for normal listening; a square root spreads them over the bars' height.
        float peak = MathF.Sqrt(_peak.Read());
        _level = Math.Max(peak, _level * Fall);
        Changed?.Invoke(this, _level);
    }
}
