namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>The default playback device's master volume.</summary>
public interface IVolumeControl : IIslandSource
{
    /// <summary>Volume from 0 to 100.</summary>
    int Level { get; }

    bool IsMuted { get; }

    /// <summary>Moves the volume by <paramref name="notches"/> steps of 2 %, unmuting if needed.</summary>
    void ChangeBy(int notches);
}
