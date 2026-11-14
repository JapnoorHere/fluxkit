namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>A paired Bluetooth device connected or disconnected. The name is shown on screen only and never logged.</summary>
public readonly record struct DeviceChange(string Name, bool Connected);

/// <summary>Paired Bluetooth devices connecting and disconnecting. Events may arrive on any thread.</summary>
public interface IDeviceSource : IIslandSource
{
    event EventHandler<DeviceChange>? Changed;
}
