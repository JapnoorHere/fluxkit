using FluxKit.Platform.Audio;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Dispatching;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// The "you're muted" nudge. While a call is muted, the microphone's level is checked four times a second (Windows'
/// own meter: nothing is recorded or kept). Talking for about three-quarters of a second tells the tool, which shows
/// the nudge at most every 30 seconds. Nothing runs when there is no muted call.
/// </summary>
public sealed partial class IslandPill
{
    private const float SpeakingLevel = 0.06f;
    private const int SpeakingSamples = 3;

    private DispatcherQueueTimer? _callTimer;
    private PeakMeter? _micMeter;
    private int _speaking;

    private void UpdateCallListener()
    {
        bool mutedCall = _active && Commands is not null
            && (_scene.Primary is { Kind: ActivityKind.Call, Flag: true } || _scene.Secondary is { Kind: ActivityKind.Call, Flag: true });
        if (mutedCall && _callTimer is not { IsRunning: true })
        {
            _micMeter ??= new PeakMeter(AudioFlow.Capture);
            _callTimer ??= CreateCallTimer();
            _speaking = 0;
            _callTimer.Start();
        }
        else if (!mutedCall && _callTimer is { IsRunning: true })
        {
            _callTimer.Stop();
            _micMeter?.Dispose();
            _micMeter = null;
        }
    }

    private DispatcherQueueTimer CreateCallTimer()
    {
        DispatcherQueueTimer timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(250);
        timer.Tick += (_, _) =>
        {
            _speaking = (_micMeter?.Read() ?? 0) > SpeakingLevel ? _speaking + 1 : 0;
            if (_speaking >= SpeakingSamples)
            {
                _speaking = 0;
                Commands?.NotifySpeakingWhileMuted();
            }
        };
        return timer;
    }
}
