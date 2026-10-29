using System.Runtime.InteropServices;

namespace FluxKit.Platform.Audio;

/// <summary>
/// The master volume of the default playback device, through the documented Core Audio API (endpointvolume.h).
/// The default device is looked up on every call, so switching speakers or headphones just works. Nothing is cached
/// between calls and nothing polls.
/// </summary>
public sealed class EndpointVolume
{
    private const int ClsctxAll = 0x17;
    private static readonly Guid AudioEndpointVolumeId = new("5CDF2C82-841E-4546-9722-0CF74078229A");
    private static readonly Guid EventContext = new("6E7C2E0E-5C9E-4D3A-9A3E-5E3E2C1B7F01");

    private readonly object _sync = new();
    private IMMDeviceEnumerator? _enumerator;

    /// <summary>Volume from 0 to 100, or null when there is no playback device.</summary>
    public int? Level => Use(v =>
    {
        Marshal.ThrowExceptionForHR(v.GetMasterVolumeLevelScalar(out float level));
        return (int?)Math.Round(level * 100);
    });

    /// <summary>True when muted; false when not, or when there is no playback device.</summary>
    public bool IsMuted => Use(v =>
    {
        Marshal.ThrowExceptionForHR(v.GetMute(out bool muted));
        return (bool?)muted;
    }) ?? false;

    /// <summary>Sets the volume to <paramref name="percent"/> (clamped to 0–100). Raising it above 0 also unmutes.</summary>
    public bool SetLevel(int percent) => Use(v =>
    {
        int clamped = Math.Clamp(percent, 0, 100);
        Guid context = EventContext;
        Marshal.ThrowExceptionForHR(v.SetMasterVolumeLevelScalar(clamped / 100f, ref context));
        if (clamped > 0)
        {
            Marshal.ThrowExceptionForHR(v.SetMute(false, ref context));
        }

        return (bool?)true;
    }) ?? false;

    private T? Use<T>(Func<IAudioEndpointVolume, T?> action)
    {
        lock (_sync)
        {
            object? activated = null;
            IMMDevice? device = null;
            try
            {
                _enumerator ??= (IMMDeviceEnumerator)new MMDeviceEnumeratorClass();

                // eRender, eMultimedia. E_NOTFOUND when no playback device is plugged in.
                if (_enumerator.GetDefaultAudioEndpoint(0, 1, out device) != 0 || device is null)
                {
                    return default;
                }

                Guid iid = AudioEndpointVolumeId;
                Marshal.ThrowExceptionForHR(device.Activate(ref iid, ClsctxAll, 0, out activated));
                return activated is IAudioEndpointVolume volume ? action(volume) : default;
            }
            catch (Exception ex) when (ex is COMException or InvalidCastException)
            {
                return default;
            }
            finally
            {
                if (activated is not null)
                {
                    Marshal.ReleaseComObject(activated);
                }

                if (device is not null)
                {
                    Marshal.ReleaseComObject(device);
                }
            }
        }
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorClass;

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(int dataFlow, int stateMask, out nint devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice? endpoint);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int clsContext, nint activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object? instance);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig]
        int RegisterControlChangeNotify(nint notify);

        [PreserveSig]
        int UnregisterControlChangeNotify(nint notify);

        [PreserveSig]
        int GetChannelCount(out uint count);

        [PreserveSig]
        int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);

        [PreserveSig]
        int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);

        [PreserveSig]
        int GetMasterVolumeLevel(out float levelDb);

        [PreserveSig]
        int GetMasterVolumeLevelScalar(out float level);

        [PreserveSig]
        int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid eventContext);

        [PreserveSig]
        int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);

        [PreserveSig]
        int GetChannelVolumeLevel(uint channel, out float levelDb);

        [PreserveSig]
        int GetChannelVolumeLevelScalar(uint channel, out float level);

        [PreserveSig]
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);

        [PreserveSig]
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}
