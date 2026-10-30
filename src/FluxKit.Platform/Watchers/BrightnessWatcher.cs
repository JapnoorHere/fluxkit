using System.Management;
using Serilog;

namespace FluxKit.Platform.Watchers;

/// <summary>
/// Raises <see cref="Changed"/> when the built-in screen's brightness changes (keys, the quick settings slider or
/// adaptive brightness). Windows pushes WmiMonitorBrightnessEvent to us; nothing polls. External monitors don't report
/// brightness this way, so on a desktop PC this simply never fires.
/// </summary>
public sealed class BrightnessWatcher(ILogger log) : IDisposable
{
    private readonly ILogger _log = log.ForContext<BrightnessWatcher>();
    private ManagementEventWatcher? _watcher;

    /// <summary>The new brightness from 0 to 100, raised on a WMI thread.</summary>
    public event EventHandler<int>? Changed;

    public void Start()
    {
        try
        {
            _watcher = new ManagementEventWatcher(new ManagementScope(@"root\WMI"), new EventQuery("SELECT * FROM WmiMonitorBrightnessEvent"));
            _watcher.EventArrived += OnEventArrived;
            _watcher.Start();
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            // No built-in panel (or WMI is unavailable): brightness moments just don't happen.
            _log.Debug(ex, "Brightness events are not available on this PC");
            Dispose();
        }
    }

    public void Dispose()
    {
        if (_watcher is null)
        {
            return;
        }

        try
        {
            _watcher.EventArrived -= OnEventArrived;
            _watcher.Stop();
        }
        catch (ManagementException)
        {
            // Already stopped.
        }

        _watcher.Dispose();
        _watcher = null;
    }

    private void OnEventArrived(object sender, EventArrivedEventArgs e)
    {
        if (e.NewEvent["Brightness"] is { } value)
        {
            Changed?.Invoke(this, Math.Clamp(Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture), 0, 100));
        }
    }
}
