using System.Runtime.InteropServices;

namespace FluxKit.Platform.Audio;

/// <summary>
/// How loud the default speakers (or microphone) are right now, from Windows' own peak meter: the same number the
/// volume mixer's green bar shows. Reading it doesn't record or touch any audio. The caller decides how often to read;
/// the meter follows the default device as it changes, re-checking it every couple of seconds.
/// </summary>
public sealed class PeakMeter(AudioFlow flow = AudioFlow.Render) : IDisposable
{
    private const long RefreshMs = 2000;

    private IMMDeviceEnumerator? _enumerator;
    private IAudioMeterInformation? _meter;
    private long _refreshedAt = long.MinValue / 2;

    /// <summary>The current peak from 0 (silence) to 1 (full scale), or 0 when there is no device.</summary>
    public float Read()
    {
        try
        {
            long now = Environment.TickCount64;
            if (_meter is null || now - _refreshedAt > RefreshMs)
            {
                Release();
                _enumerator ??= CoreAudio.CreateEnumerator();
                _meter = CoreAudio.DefaultMeter(_enumerator, flow);
                _refreshedAt = now;
            }

            return _meter is not null && _meter.GetPeakValue(out float peak) == 0 ? Math.Clamp(peak, 0, 1) : 0;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            Release();
            return 0;
        }
    }

    public void Dispose()
    {
        Release();
        if (_enumerator is not null)
        {
            Marshal.ReleaseComObject(_enumerator);
            _enumerator = null;
        }
    }

    private void Release()
    {
        if (_meter is not null)
        {
            Marshal.ReleaseComObject(_meter);
            _meter = null;
        }
    }
}
