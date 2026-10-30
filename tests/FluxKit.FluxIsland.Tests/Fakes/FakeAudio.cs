using FluxKit.Tools.FluxIsland.Abstractions;

namespace FluxKit.FluxIsland.Tests.Fakes;

internal sealed class FakeAudio : IAudioSource
{
    public bool MicMuted { get; private set; }

    public event EventHandler<AudioLevel>? VolumeChanged;

    public event EventHandler<string>? OutputChanged;

    public event EventHandler<bool>? MicMuteChanged;

    public bool SetMicMuted(bool muted)
    {
        MicMuted = muted;
        MicMuteChanged?.Invoke(this, muted);
        return true;
    }

    public void ChangeVolume(int level, bool muted) => VolumeChanged?.Invoke(this, new AudioLevel(level, muted));

    public void SwitchOutput(string name) => OutputChanged?.Invoke(this, name);

    public void Start()
    {
    }

    public void Dispose()
    {
    }
}
