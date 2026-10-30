using FluxKit.Tools.FluxIsland.Abstractions;
using Windows.Devices.Power;
using Windows.System.Power;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary><see cref="IPowerSource"/> over the Windows power manager events.</summary>
internal sealed class PowerSource : IPowerSource
{
    /// <summary>Estimates beyond this are Windows saying "don't know" (it reports a huge value while plugged in).</summary>
    private static readonly TimeSpan LongestEstimate = TimeSpan.FromHours(48);

    public PowerSnapshot Current
    {
        get
        {
            bool hasBattery = PowerManager.BatteryStatus != BatteryStatus.NotPresent;
            bool pluggedIn = PowerManager.PowerSupplyStatus != PowerSupplyStatus.NotPresent;
            return new PowerSnapshot(
                hasBattery,
                hasBattery ? PowerManager.RemainingChargePercent : 0,
                pluggedIn,
                PowerManager.EnergySaverStatus == EnergySaverStatus.On,
                hasBattery ? (pluggedIn ? TimeToFull() : TimeToEmpty()) : null);
        }
    }

    public event EventHandler? Changed;

    public void Start()
    {
        PowerManager.BatteryStatusChanged += OnChanged;
        PowerManager.PowerSupplyStatusChanged += OnChanged;
        PowerManager.RemainingChargePercentChanged += OnChanged;
        PowerManager.EnergySaverStatusChanged += OnChanged;
    }

    public void Dispose()
    {
        PowerManager.BatteryStatusChanged -= OnChanged;
        PowerManager.PowerSupplyStatusChanged -= OnChanged;
        PowerManager.RemainingChargePercentChanged -= OnChanged;
        PowerManager.EnergySaverStatusChanged -= OnChanged;
    }

    private static TimeSpan? TimeToEmpty()
    {
        TimeSpan left = PowerManager.RemainingDischargeTime;
        return left > TimeSpan.Zero && left < LongestEstimate ? left : null;
    }

    /// <summary>From the battery's own report: the energy still missing divided by the rate it is charging at.</summary>
    private static TimeSpan? TimeToFull()
    {
        BatteryReport report = Battery.AggregateBattery.GetReport();
        if (report.ChargeRateInMilliwatts is not > 0 || report.FullChargeCapacityInMilliwattHours is not { } full || report.RemainingCapacityInMilliwattHours is not { } remaining || remaining >= full)
        {
            return null;
        }

        TimeSpan left = TimeSpan.FromHours((full - remaining) / (double)report.ChargeRateInMilliwatts.Value);
        return left < LongestEstimate ? left : null;
    }

    private void OnChanged(object? sender, object e) => Changed?.Invoke(this, EventArgs.Empty);
}
