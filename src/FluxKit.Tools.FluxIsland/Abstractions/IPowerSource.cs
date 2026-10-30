namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>Battery and charger state. On a PC without a battery, <see cref="PowerSnapshot.HasBattery"/> is false.</summary>
public interface IPowerSource : IIslandSource
{
    PowerSnapshot Current { get; }

    event EventHandler? Changed;
}

/// <summary>
/// The battery right now. <paramref name="SaverOn"/> is Windows battery saver (energy saver); <paramref name="TimeLeft"/> is
/// the time until empty on battery, or until full while charging, when Windows can estimate it.
/// </summary>
public readonly record struct PowerSnapshot(bool HasBattery, int Percent, bool IsPluggedIn, bool SaverOn = false, TimeSpan? TimeLeft = null);
