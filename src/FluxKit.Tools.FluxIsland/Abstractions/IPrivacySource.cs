namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>Whether any app is using the microphone or camera right now. Never says which app.</summary>
public interface IPrivacySource : IIslandSource
{
    PrivacySnapshot Current { get; }

    event EventHandler? Changed;
}

public readonly record struct PrivacySnapshot(bool MicrophoneInUse, bool CameraInUse);
