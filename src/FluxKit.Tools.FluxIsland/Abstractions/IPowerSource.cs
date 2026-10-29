namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>Battery and charger state. On a PC without a battery, <see cref="PowerSnapshot.HasBattery"/> is false.</summary>
public interface IPowerSource : IIslandSource
{
    PowerSnapshot Current { get; }

    event EventHandler? Changed;
}

public readonly record struct PowerSnapshot(bool HasBattery, int Percent, bool IsPluggedIn);
