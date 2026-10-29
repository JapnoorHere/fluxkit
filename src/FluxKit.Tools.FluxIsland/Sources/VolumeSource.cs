using FluxKit.Platform.Audio;
using FluxKit.Tools.FluxIsland.Abstractions;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary><see cref="IVolumeControl"/> backed by the Platform <see cref="EndpointVolume"/>. It only acts when asked.</summary>
internal sealed class VolumeSource : IVolumeControl
{
    private const int Step = 2;

    private readonly EndpointVolume _endpoint = new();

    public int Level => _endpoint.Level ?? 0;

    public bool IsMuted => _endpoint.IsMuted;

    public void ChangeBy(int notches)
    {
        if (notches == 0 || _endpoint.Level is not { } current)
        {
            return;
        }

        _endpoint.SetLevel(StepTarget(current, notches));
    }

    /// <summary>
    /// The level after <paramref name="notches"/> steps. It lands on even steps, like the Windows volume keys, so
    /// scrolling up and back returns to the same level.
    /// </summary>
    internal static int StepTarget(int current, int notches)
    {
        double steps = (current + (notches * Step)) / (double)Step;
        int even = (int)(notches > 0 ? Math.Floor(steps) : Math.Ceiling(steps)) * Step;
        return Math.Clamp(even, 0, 100);
    }

    public void Start()
    {
    }

    public void Dispose()
    {
    }
}
