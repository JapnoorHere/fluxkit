using FluxKit.Platform.Audio;
using FluxKit.Tools.FluxIsland.Abstractions;
using Serilog;
using Windows.Devices.Enumeration;
using Windows.Media.Devices;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary>
/// <see cref="IAudioSource"/> backed by Core Audio callbacks (<see cref="AudioEndpointWatcher"/>) and the WinRT default
/// device events. When the default device changes, the watchers move to the new one. Nothing polls.
/// </summary>
internal sealed class AudioSource : IAudioSource
{
    private readonly ILogger _log;
    private readonly AudioEndpointWatcher _output = new(AudioFlow.Render);
    private readonly AudioEndpointWatcher _input = new(AudioFlow.Capture);
    private readonly EndpointVolume _microphone = new(AudioFlow.Capture);
    private bool? _micMuted;
    private bool _started;

    public AudioSource(ILogger log)
    {
        _log = log.ForContext<AudioSource>();
        _output.Changed += (_, e) => VolumeChanged?.Invoke(this, new AudioLevel(e.Level, e.Muted));
        _input.Changed += (_, e) => OnMicChanged(e.Muted);
    }

    public bool MicMuted => _microphone.IsMuted;

    public event EventHandler<AudioLevel>? VolumeChanged;

    public event EventHandler<string>? OutputChanged;

    public event EventHandler<bool>? MicMuteChanged;

    public bool SetMicMuted(bool muted) => _microphone.SetMuted(muted);

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _micMuted = _microphone.IsMuted;
        _output.Rebind();
        _input.Rebind();
        MediaDevice.DefaultAudioRenderDeviceChanged += OnRenderDeviceChanged;
        MediaDevice.DefaultAudioCaptureDeviceChanged += OnCaptureDeviceChanged;
    }

    public void Dispose()
    {
        if (_started)
        {
            MediaDevice.DefaultAudioRenderDeviceChanged -= OnRenderDeviceChanged;
            MediaDevice.DefaultAudioCaptureDeviceChanged -= OnCaptureDeviceChanged;
        }

        _output.Dispose();
        _input.Dispose();
    }

    private void OnMicChanged(bool muted)
    {
        // The callback also fires when the microphone level moves; only a change of mute matters here.
        if (_micMuted == muted)
        {
            return;
        }

        _micMuted = muted;
        MicMuteChanged?.Invoke(this, muted);
    }

    private void OnCaptureDeviceChanged(object sender, DefaultAudioCaptureDeviceChangedEventArgs args)
    {
        if (args.Role == AudioDeviceRole.Default)
        {
            _input.Rebind();
            OnMicChanged(_microphone.IsMuted);
        }
    }

    private void OnRenderDeviceChanged(object sender, DefaultAudioRenderDeviceChangedEventArgs args)
    {
        if (args.Role == AudioDeviceRole.Default)
        {
            _output.Rebind();
            _ = AnnounceOutputAsync(args.Id);
        }
    }

    private async Task AnnounceOutputAsync(string id)
    {
        try
        {
            DeviceInformation device = await DeviceInformation.CreateFromIdAsync(id);
            OutputChanged?.Invoke(this, device.Name);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Reading the new playback device failed");
        }
    }
}
