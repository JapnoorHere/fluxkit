using FluxKit.Tools.FluxIsland.Abstractions;
using Serilog;
using Windows.Devices.Enumeration;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary>
/// <see cref="IDeviceSource"/> over a Windows device watcher for paired Bluetooth devices (classic and Low Energy).
/// Windows pushes connection changes; nothing polls. Devices already connected when FluxKit starts are not announced.
/// Names are shown on screen only and never logged.
/// </summary>
internal sealed class BluetoothSource(ILogger log) : IDeviceSource
{
    private const string IsConnected = "System.Devices.Aep.IsConnected";

    // Paired association endpoints using the Bluetooth or Bluetooth LE protocol.
    private const string Selector =
        "System.Devices.Aep.IsPaired:=System.StructuredQueryType.Boolean#True AND " +
        "(System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\" OR " +
        "System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";

    /// <summary>Headsets often show up as both a classic and an LE device; one change should give one moment.</summary>
    private static readonly TimeSpan Coalesce = TimeSpan.FromSeconds(3);

    private readonly ILogger _log = log.ForContext<BluetoothSource>();
    private readonly Lock _sync = new();
    private readonly Dictionary<string, (string Name, bool Connected)> _devices = [];
    private DeviceWatcher? _watcher;
    private bool _ready;
    private (string Name, bool Connected, long At) _last;

    public event EventHandler<DeviceChange>? Changed;

    public void Start()
    {
        try
        {
            _watcher = DeviceInformation.CreateWatcher(Selector, [IsConnected], DeviceInformationKind.AssociationEndpoint);
            _watcher.Added += OnAdded;
            _watcher.Updated += OnUpdated;
            _watcher.Removed += OnRemoved;
            _watcher.EnumerationCompleted += OnEnumerationCompleted;
            _watcher.Start();
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or InvalidOperationException)
        {
            // No Bluetooth radio, or the service is off: device moments just don't happen.
            _log.Debug(ex, "Bluetooth device events are not available on this PC");
            Dispose();
        }
    }

    public void Dispose()
    {
        DeviceWatcher? watcher = _watcher;
        _watcher = null;
        if (watcher is null)
        {
            return;
        }

        watcher.Added -= OnAdded;
        watcher.Updated -= OnUpdated;
        watcher.Removed -= OnRemoved;
        watcher.EnumerationCompleted -= OnEnumerationCompleted;
        try
        {
            if (watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
            {
                watcher.Stop();
            }
        }
        catch (InvalidOperationException)
        {
            // Already stopping.
        }
    }

    private static bool ReadConnected(IReadOnlyDictionary<string, object> properties) =>
        properties.TryGetValue(IsConnected, out object? value) && value is true;

    private void OnAdded(DeviceWatcher sender, DeviceInformation device)
    {
        bool connected = ReadConnected(device.Properties);
        bool announce;
        lock (_sync)
        {
            _devices[device.Id] = (device.Name, connected);
            announce = _ready && connected;
        }

        if (announce)
        {
            Raise(device.Name, connected: true);
        }
    }

    private void OnUpdated(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        if (!update.Properties.ContainsKey(IsConnected))
        {
            return;
        }

        bool connected = ReadConnected(update.Properties);
        string? name = null;
        lock (_sync)
        {
            if (_devices.TryGetValue(update.Id, out (string Name, bool Connected) known) && known.Connected != connected)
            {
                _devices[update.Id] = (known.Name, connected);
                name = _ready ? known.Name : null;
            }
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            Raise(name, connected);
        }
    }

    private void OnRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        lock (_sync)
        {
            _devices.Remove(update.Id);
        }
    }

    private void OnEnumerationCompleted(DeviceWatcher sender, object args)
    {
        lock (_sync)
        {
            _ready = true;
        }
    }

    private void Raise(string name, bool connected)
    {
        long now = Environment.TickCount64;
        lock (_sync)
        {
            if (_last.Name == name && _last.Connected == connected && now - _last.At < Coalesce.TotalMilliseconds)
            {
                return;
            }

            _last = (name, connected, now);
        }

        Changed?.Invoke(this, new DeviceChange(name, connected));
    }
}
