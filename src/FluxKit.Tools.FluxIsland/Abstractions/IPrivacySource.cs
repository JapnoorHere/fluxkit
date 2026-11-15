namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>Whether any app is using the microphone or camera right now. Never says which app.</summary>
public interface IPrivacySource : IIslandSource
{
    PrivacySnapshot Current { get; }

    event EventHandler? Changed;
}

/// <param name="CallInProgress">A call app (Teams, Zoom, a browser…) is the one using the microphone.</param>
public readonly record struct PrivacySnapshot(bool MicrophoneInUse, bool CameraInUse, bool CallInProgress = false);
