namespace FluxKit.Platform.Audio;

/// <summary>Which way sound goes, as Core Audio numbers it (EDataFlow).</summary>
public enum AudioFlow
{
    /// <summary>Playback: speakers and headphones.</summary>
    Render = 0,

    /// <summary>Recording: microphones.</summary>
    Capture = 1,
}
