using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Serilog;

namespace FluxKit.Platform.Watchers;

/// <summary>
/// Tells whether any app is using the microphone or the camera right now, from the same per-user consent store the
/// Windows privacy indicator uses. The registry tells us when it changes, so nothing is polled. App names are read
/// only to walk the store; they are never kept or logged.
/// </summary>
public sealed partial class PrivacyWatcher : IDisposable
{
    private const string ConsentStore = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\";
    private const int RegNotifyChangeName = 0x1;
    private const int RegNotifyChangeLastSet = 0x4;

    // Apps write several values when they start or stop using a device; wait for the burst to settle before reading.
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(150);

    private readonly ILogger _log;
    private readonly ManualResetEvent _stop = new(false);
    private Thread? _thread;
    private volatile bool _microphone;
    private volatile bool _camera;

    public PrivacyWatcher(ILogger log) => _log = log.ForContext<PrivacyWatcher>();

    public bool MicrophoneInUse => _microphone;

    public bool CameraInUse => _camera;

    /// <summary>Raised on the watcher thread when either flag changes.</summary>
    public event EventHandler? Changed;

    public void Start()
    {
        if (_thread is not null)
        {
            return;
        }

        _stop.Reset();
        _thread = new Thread(Run) { IsBackground = true, Name = "FluxKit.PrivacyWatcher" };
        _thread.Start();
    }

    public void Dispose()
    {
        _stop.Set();
        _thread?.Join(TimeSpan.FromSeconds(1));
        _thread = null;
    }

    private void Run()
    {
        try
        {
            using RegistryKey? microphone = Registry.CurrentUser.OpenSubKey(ConsentStore + "microphone");
            using RegistryKey? camera = Registry.CurrentUser.OpenSubKey(ConsentStore + "webcam");
            if (microphone is null && camera is null)
            {
                _log.Information("No microphone or camera consent store; privacy dots are off");
                return;
            }

            using var microphoneChanged = new AutoResetEvent(false);
            using var cameraChanged = new AutoResetEvent(false);
            Arm(microphone, microphoneChanged);
            Arm(camera, cameraChanged);
            Evaluate(microphone, camera);

            WaitHandle[] handles = [_stop, microphoneChanged, cameraChanged];
            while (true)
            {
                int fired = WaitHandle.WaitAny(handles);
                if (fired == 0 || _stop.WaitOne(Settle))
                {
                    break;
                }

                // A notification fires once; only the one that fired needs to be asked for again.
                if (fired == 1)
                {
                    Arm(microphone, microphoneChanged);
                }
                else
                {
                    Arm(camera, cameraChanged);
                }

                Evaluate(microphone, camera);
            }
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Privacy watcher stopped");
        }
    }

    private void Evaluate(RegistryKey? microphone, RegistryKey? camera)
    {
        bool mic = InUse(microphone);
        bool cam = InUse(camera);
        if (mic == _microphone && cam == _camera)
        {
            return;
        }

        _microphone = mic;
        _camera = cam;
        _log.Debug("Microphone in use: {Microphone}, camera in use: {Camera}", mic, cam);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Arm(RegistryKey? key, AutoResetEvent signal)
    {
        if (key is null)
        {
            return;
        }

        int result = RegNotifyChangeKeyValue(key.Handle, true, RegNotifyChangeName | RegNotifyChangeLastSet, signal.SafeWaitHandle, true);
        if (result != 0)
        {
            _log.Warning("Registry change notification failed ({Result})", result);
        }
    }

    /// <summary>An app is using the device while its last use has a start time but no stop time yet.</summary>
    internal static bool InUse(RegistryKey? store)
    {
        if (store is null)
        {
            return false;
        }

        foreach (string name in store.GetSubKeyNames())
        {
            using RegistryKey? entry = store.OpenSubKey(name);
            if (entry is null)
            {
                continue;
            }

            // Desktop apps sit one level deeper, under "NonPackaged".
            if (name == "NonPackaged")
            {
                foreach (string app in entry.GetSubKeyNames())
                {
                    using RegistryKey? desktopApp = entry.OpenSubKey(app);
                    if (IsActive(desktopApp))
                    {
                        return true;
                    }
                }
            }
            else if (IsActive(entry))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsActive(RegistryKey? app) =>
        app?.GetValue("LastUsedTimeStop") is long stop && stop == 0
        && app.GetValue("LastUsedTimeStart") is long start && start > 0;

    [LibraryImport("advapi32.dll")]
    private static partial int RegNotifyChangeKeyValue(SafeRegistryHandle key, [MarshalAs(UnmanagedType.Bool)] bool watchSubtree, int filter, SafeWaitHandle signal, [MarshalAs(UnmanagedType.Bool)] bool asynchronous);
}
