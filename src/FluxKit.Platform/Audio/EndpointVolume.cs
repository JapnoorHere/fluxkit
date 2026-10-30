using System.Runtime.InteropServices;

namespace FluxKit.Platform.Audio;

/// <summary>
/// The master volume and mute of the default playback or recording device, through the documented Core Audio API.
/// The default device is looked up on every call, so switching speakers, headphones or microphones just works.
/// Nothing is cached between calls and nothing polls.
/// </summary>
public sealed class EndpointVolume(AudioFlow flow = AudioFlow.Render)
{
    private readonly object _sync = new();
    private IMMDeviceEnumerator? _enumerator;

    /// <summary>Volume from 0 to 100, or null when there is no device.</summary>
    public int? Level => Use(v =>
    {
        Marshal.ThrowExceptionForHR(v.GetMasterVolumeLevelScalar(out float level));
        return (int?)Math.Round(level * 100);
    });

    /// <summary>True when muted; false when not, or when there is no device.</summary>
    public bool IsMuted => Use(v =>
    {
        Marshal.ThrowExceptionForHR(v.GetMute(out bool muted));
        return (bool?)muted;
    }) ?? false;

    /// <summary>Sets the volume to <paramref name="percent"/> (clamped to 0–100). Raising it above 0 also unmutes.</summary>
    public bool SetLevel(int percent) => Use(v =>
    {
        int clamped = Math.Clamp(percent, 0, 100);
        Guid context = CoreAudio.EventContext;
        Marshal.ThrowExceptionForHR(v.SetMasterVolumeLevelScalar(clamped / 100f, ref context));
        if (clamped > 0)
        {
            Marshal.ThrowExceptionForHR(v.SetMute(false, ref context));
        }

        return (bool?)true;
    }) ?? false;

    /// <summary>Mutes or unmutes the device. Returns false when there is no device.</summary>
    public bool SetMuted(bool muted) => Use(v =>
    {
        Guid context = CoreAudio.EventContext;
        Marshal.ThrowExceptionForHR(v.SetMute(muted, ref context));
        return (bool?)true;
    }) ?? false;

    private T? Use<T>(Func<IAudioEndpointVolume, T?> action)
    {
        lock (_sync)
        {
            IAudioEndpointVolume? volume = null;
            try
            {
                _enumerator ??= CoreAudio.CreateEnumerator();
                volume = CoreAudio.DefaultEndpoint(_enumerator, flow);
                return volume is null ? default : action(volume);
            }
            catch (Exception ex) when (ex is COMException or InvalidCastException)
            {
                return default;
            }
            finally
            {
                if (volume is not null)
                {
                    Marshal.ReleaseComObject(volume);
                }
            }
        }
    }
}
