using FluxKit.Tools.FluxIsland.Abstractions;
using Windows.System.Power;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary><see cref="IPowerSource"/> over the Windows power manager events.</summary>
internal sealed class PowerSource : IPowerSource
{
    public PowerSnapshot Current
    {
        get
        {
            bool hasBattery = PowerManager.BatteryStatus != BatteryStatus.NotPresent;
            return new PowerSnapshot(hasBattery, hasBattery ? PowerManager.RemainingChargePercent : 0, PowerManager.PowerSupplyStatus != PowerSupplyStatus.NotPresent);
        }
    }

    public event EventHandler? Changed;

    public void Start()
    {
        PowerManager.BatteryStatusChanged += OnChanged;
        PowerManager.PowerSupplyStatusChanged += OnChanged;
        PowerManager.RemainingChargePercentChanged += OnChanged;
    }

    public void Dispose()
    {
        PowerManager.BatteryStatusChanged -= OnChanged;
        PowerManager.PowerSupplyStatusChanged -= OnChanged;
        PowerManager.RemainingChargePercentChanged -= OnChanged;
    }

    private void OnChanged(object? sender, object e) => Changed?.Invoke(this, EventArgs.Empty);
}
