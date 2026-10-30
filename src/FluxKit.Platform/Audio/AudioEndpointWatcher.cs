using System.Runtime.InteropServices;

namespace FluxKit.Platform.Audio;

/// <summary>
/// Raises <see cref="Changed"/> whenever the default device's volume or mute changes, by any means: the keyboard's
/// volume keys, the Windows flyout, another app or FluxKit itself. Core Audio calls back on its own thread; nothing
/// polls. Call <see cref="Rebind"/> when the default device changes.
/// </summary>
public sealed class AudioEndpointWatcher(AudioFlow flow) : IDisposable
{
    private readonly object _sync = new();
    private readonly Callback _callback = new();
    private IMMDeviceEnumerator? _enumerator;
    private IAudioEndpointVolume? _volume;
    private bool _disposed;

    /// <summary>Level 0–100 and mute state, raised on a Core Audio thread.</summary>
    public event EventHandler<(int Level, bool Muted)>? Changed;

    /// <summary>Starts listening to the current default device (and stops listening to the previous one).</summary>
    public void Rebind()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            Release();
            try
            {
                _enumerator ??= CoreAudio.CreateEnumerator();
                _volume = CoreAudio.DefaultEndpoint(_enumerator, flow);
                _callback.Owner = this;
                if (_volume is not null)
                {
                    Marshal.ThrowExceptionForHR(_volume.RegisterControlChangeNotify(_callback));
                }
            }
            catch (Exception ex) when (ex is COMException or InvalidCastException)
            {
                Release();
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _disposed = true;
            Release();
            _callback.Owner = null;
        }
    }

    private void Release()
    {
        if (_volume is null)
        {
            return;
        }

        try
        {
            _volume.UnregisterControlChangeNotify(_callback);
        }
        catch (COMException)
        {
            // The device is already gone; nothing to unregister from.
        }

        Marshal.ReleaseComObject(_volume);
        _volume = null;
    }

    private void Raise(int level, bool muted) => Changed?.Invoke(this, (level, muted));

    /// <summary>The COM object Core Audio calls. It reads AUDIO_VOLUME_NOTIFICATION_DATA: a GUID, then BOOL bMuted, then float fMasterVolume.</summary>
    private sealed class Callback : IAudioEndpointVolumeCallback
    {
        public AudioEndpointWatcher? Owner { get; set; }

        public int OnNotify(nint notifyData)
        {
            if (notifyData != 0 && Owner is { } owner)
            {
                bool muted = Marshal.ReadInt32(notifyData, 16) != 0;
                float level = BitConverter.Int32BitsToSingle(Marshal.ReadInt32(notifyData, 20));
                owner.Raise((int)Math.Round(level * 100), muted);
            }

            return 0;
        }
    }
}
