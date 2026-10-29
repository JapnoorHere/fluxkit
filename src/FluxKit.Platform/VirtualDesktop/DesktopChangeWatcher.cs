using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Serilog;

namespace FluxKit.Platform.VirtualDesktop;

/// <summary>
/// Event-driven watcher for an HKCU registry subtree (RegNotifyChangeKeyValue). No polling; bursts are
/// debounced so one desktop operation produces one notification.
/// </summary>
internal sealed partial class DesktopChangeWatcher : IDisposable
{
    private const int RegNotifyChangeName = 0x1;
    private const int RegNotifyChangeLastSet = 0x4;
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(150);

    private readonly string _keyPath;
    private readonly Action _onChanged;
    private readonly ILogger _log;
    private readonly ManualResetEvent _stop = new(false);
    private Thread? _thread;

    public DesktopChangeWatcher(string keyPath, Action onChanged, ILogger log)
    {
        _keyPath = keyPath;
        _onChanged = onChanged;
        _log = log.ForContext<DesktopChangeWatcher>();
    }

    public void Start()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "FluxKit.DesktopWatcher" };
        _thread.Start();
    }

    public void Dispose()
    {
        _stop.Set();
        _thread?.Join(TimeSpan.FromSeconds(1));
        _stop.Dispose();
    }

    private void Run()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_keyPath);
        if (key is null)
        {
            _log.Warning("Virtual desktop registry key not found; change notifications disabled");
            return;
        }

        using var changed = new AutoResetEvent(false);
        WaitHandle[] handles = [_stop, changed];
        while (true)
        {
            int rc = RegNotifyChangeKeyValue(key.Handle, true, RegNotifyChangeName | RegNotifyChangeLastSet, changed.SafeWaitHandle, true);
            if (rc != 0)
            {
                _log.Warning("RegNotifyChangeKeyValue failed ({Code}); change notifications disabled", rc);
                return;
            }

            if (WaitHandle.WaitAny(handles) == 0)
            {
                return;
            }

            // Coalesce the burst of writes Explorer makes for a single operation.
            while (WaitHandle.WaitAny(handles, Debounce) == 1)
            {
                _ = RegNotifyChangeKeyValue(key.Handle, true, RegNotifyChangeName | RegNotifyChangeLastSet, changed.SafeWaitHandle, true);
            }

            if (_stop.WaitOne(0))
            {
                return;
            }

            try
            {
                _onChanged();
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Desktop change handler failed");
            }
        }
    }

    [LibraryImport("advapi32.dll")]
    private static partial int RegNotifyChangeKeyValue(SafeRegistryHandle key, [MarshalAs(UnmanagedType.Bool)] bool watchSubtree, int filter, SafeWaitHandle evt, [MarshalAs(UnmanagedType.Bool)] bool asynchronous);
}
