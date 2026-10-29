using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;

namespace FluxKit.FluxIsland.Tests;

public sealed class BatteryAlertPolicyTests
{
    private readonly BatteryAlertPolicy _policy = new();

    [Fact]
    public void First_reading_never_says_plugged_in()
    {
        Assert.Null(_policy.Evaluate(new PowerSnapshot(true, 80, IsPluggedIn: true)));
    }

    [Fact]
    public void Plugging_in_and_unplugging_raise_alerts()
    {
        _policy.Evaluate(new PowerSnapshot(true, 60, false));

        Assert.Equal(ActivityKind.PluggedIn, _policy.Evaluate(new PowerSnapshot(true, 60, true)));
        Assert.Null(_policy.Evaluate(new PowerSnapshot(true, 61, true)));
        Assert.Equal(ActivityKind.Unplugged, _policy.Evaluate(new PowerSnapshot(true, 61, false)));
    }

    [Fact]
    public void Low_battery_is_raised_once_per_discharge()
    {
        _policy.Evaluate(new PowerSnapshot(true, 22, false));

        Assert.Equal(ActivityKind.LowBattery, _policy.Evaluate(new PowerSnapshot(true, 20, false)));
        Assert.Null(_policy.Evaluate(new PowerSnapshot(true, 19, false)));
        Assert.Null(_policy.Evaluate(new PowerSnapshot(true, 12, false)));
    }

    [Fact]
    public void Charging_rearms_the_low_battery_alert()
    {
        _policy.Evaluate(new PowerSnapshot(true, 20, false));
        _policy.Evaluate(new PowerSnapshot(true, 20, true));

        Assert.Equal(ActivityKind.Unplugged, _policy.Evaluate(new PowerSnapshot(true, 19, false)));
        Assert.Equal(ActivityKind.LowBattery, _policy.Evaluate(new PowerSnapshot(true, 18, false)));
    }

    [Fact]
    public void Pc_without_a_battery_never_alerts()
    {
        Assert.Null(_policy.Evaluate(new PowerSnapshot(false, 0, true)));
        Assert.Null(_policy.Evaluate(new PowerSnapshot(false, 0, false)));
    }
}
