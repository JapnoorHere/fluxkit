namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>
/// Sound changes made anywhere in Windows: the volume or mute of the playback device, which device is playing, and
/// whether the microphone is muted. Events may arrive on any thread.
/// </summary>
public interface IAudioSource : IIslandSource
{
    /// <summary>True when the default microphone is muted; false when not, or when there is no microphone.</summary>
    bool MicMuted { get; }

    /// <summary>The volume (0–100) or mute of the playback device changed.</summary>
    event EventHandler<AudioLevel>? VolumeChanged;

    /// <summary>Sound now plays on another device. Carries its friendly name, shown on screen only.</summary>
    event EventHandler<string>? OutputChanged;

    /// <summary>The microphone was muted (true) or unmuted (false), by FluxKit or anything else.</summary>
    event EventHandler<bool>? MicMuteChanged;

    /// <summary>Mutes or unmutes the default microphone. Returns false when there is no microphone.</summary>
    bool SetMicMuted(bool muted);
}

public readonly record struct AudioLevel(int Level, bool Muted);
