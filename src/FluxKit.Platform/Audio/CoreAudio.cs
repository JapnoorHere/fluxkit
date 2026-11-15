using System.Runtime.InteropServices;

namespace FluxKit.Platform.Audio;

/// <summary>The documented Core Audio declarations (mmdeviceapi.h, endpointvolume.h) shared by the audio helpers.</summary>
internal static class CoreAudio
{
    public const int ClsctxAll = 0x17;
    public static readonly Guid AudioEndpointVolumeId = new("5CDF2C82-841E-4546-9722-0CF74078229A");
    public static readonly Guid AudioMeterInformationId = new("C02216F6-8C67-4B5B-9D00-D008E73E0064");

    /// <summary>Tags FluxKit's own changes, so a watcher can tell them apart if it needs to.</summary>
    public static readonly Guid EventContext = new("6E7C2E0E-5C9E-4D3A-9A3E-5E3E2C1B7F01");

    /// <summary>The default endpoint's volume interface for <paramref name="flow"/>, or null when there is no such device.</summary>
    public static IAudioEndpointVolume? DefaultEndpoint(IMMDeviceEnumerator enumerator, AudioFlow flow) =>
        Activate(enumerator, flow, AudioEndpointVolumeId) as IAudioEndpointVolume;

    /// <summary>The default endpoint's peak meter for <paramref name="flow"/>, or null when there is no such device.</summary>
    public static IAudioMeterInformation? DefaultMeter(IMMDeviceEnumerator enumerator, AudioFlow flow) =>
        Activate(enumerator, flow, AudioMeterInformationId) as IAudioMeterInformation;

    /// <summary>Activates interface <paramref name="iid"/> on the default endpoint for <paramref name="flow"/>.</summary>
    private static object? Activate(IMMDeviceEnumerator enumerator, AudioFlow flow, Guid iid)
    {
        IMMDevice? device = null;
        try
        {
            // Role eMultimedia. E_NOTFOUND when no device of that kind is plugged in.
            if (enumerator.GetDefaultAudioEndpoint((int)flow, 1, out device) != 0 || device is null)
            {
                return null;
            }

            Marshal.ThrowExceptionForHR(device.Activate(ref iid, ClsctxAll, 0, out object? activated));
            return activated;
        }
        finally
        {
            if (device is not null)
            {
                Marshal.ReleaseComObject(device);
            }
        }
    }

    public static IMMDeviceEnumerator CreateEnumerator() => (IMMDeviceEnumerator)new MMDeviceEnumeratorClass();

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorClass;
}

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig]
    int EnumAudioEndpoints(int dataFlow, int stateMask, out nint devices);

    [PreserveSig]
    int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice? endpoint);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig]
    int Activate(ref Guid iid, int clsContext, nint activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object? instance);
}

[ComImport]
[Guid("657804FA-D6AD-4496-8A60-352752AF4F89")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolumeCallback
{
    [PreserveSig]
    int OnNotify(nint notifyData);
}

[ComImport]
[Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    [PreserveSig]
    int RegisterControlChangeNotify(IAudioEndpointVolumeCallback notify);

    [PreserveSig]
    int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback notify);

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

[ComImport]
[Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioMeterInformation
{
    [PreserveSig]
    int GetPeakValue(out float peak);
}
