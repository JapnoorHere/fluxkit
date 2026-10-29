using FluxKit.Tools.FluxIsland.Abstractions;

namespace FluxKit.Tools.FluxIsland.Activities;

/// <summary>
/// Decides when power changes deserve an alert: plugging in, unplugging, and the battery reaching
/// <see cref="LowPercent"/>. The low alert fires once per discharge and re-arms after charging.
/// </summary>
internal sealed class BatteryAlertPolicy
{
    public const int LowPercent = 20;

    /// <summary>How far above <see cref="LowPercent"/> the battery must climb before the low alert can fire again.</summary>
    private const int Rearm = 5;

    private PowerSnapshot? _last;
    private bool _lowShown;

    public ActivityKind? Evaluate(PowerSnapshot now)
    {
        PowerSnapshot? last = _last;
        _last = now;
        if (!now.HasBattery)
        {
            return null;
        }

        ActivityKind? alert = null;
        if (last is { HasBattery: true } previous && previous.IsPluggedIn != now.IsPluggedIn)
        {
            alert = now.IsPluggedIn ? ActivityKind.PluggedIn : ActivityKind.Unplugged;
        }

        if (now.IsPluggedIn || now.Percent > LowPercent + Rearm)
        {
            _lowShown = false;
        }
        else if (!_lowShown && now.Percent <= LowPercent && alert is null)
        {
            // When unplugging already raised an alert, the low warning waits for the next reading.
            _lowShown = true;
            alert = ActivityKind.LowBattery;
        }

        return alert;
    }
}
