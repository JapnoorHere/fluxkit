using System.Reflection;
using FluxKit.App.Modules.DragToSwitch;
using FluxKit.App.Shell;
using FluxKit.App.Theming;
using FluxKit.Core.Logging;
using FluxKit.Core.Settings;
using FluxKit.Core.Utilities;
using FluxKit.Platform.Input;
using FluxKit.Platform.Shell;
using FluxKit.Platform.Startup;
using FluxKit.Platform.VirtualDesktop;
using FluxKit.Utilities.DragToSwitch;
using FluxKit.Utilities.DragToSwitch.Interaction;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Serilog;

namespace FluxKit.App;

/// <summary>
/// Composition root and application lifecycle: settings, logging, theme, virtual desktops, utilities,
/// tray icon and windows. Pages reach shared services through <see cref="Current"/>.
/// </summary>
public sealed class AppHost
{
    private readonly DispatcherQueue _dispatcher;
    private MainWindow? _mainWindow;
    private SelectorWindow? _selector;
    private TrayIcon? _tray;
    private bool _exiting;

    public AppHost(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
        Current = this;
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        DataDirectory = Path.Combine(local, "FluxKit");
        LogDirectory = Path.Combine(DataDirectory, "Logs");
        SettingsPath = Path.Combine(DataDirectory, "settings.json");
        Version = typeof(AppHost).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0] ?? "1.0.0";
    }

    public static AppHost Current { get; private set; } = null!;

    public string DataDirectory { get; }

    public string LogDirectory { get; }

    public string SettingsPath { get; }

    public string Version { get; }

    public ILogger Log { get; private set; } = Serilog.Log.Logger;

    public SettingsService Settings { get; private set; } = null!;

    public IVirtualDesktopService Desktops { get; private set; } = null!;

    public UtilityRegistry Registry { get; private set; } = null!;

    public DragToSwitchUtility DragToSwitch { get; private set; } = null!;

    public StartupRegistration Startup { get; } = new("FluxKit", Environment.ProcessPath ?? "FluxKit.exe");

    public SelectorWindow? Selector => _selector;

    public event EventHandler? ThemeChanged;

    public event EventHandler? StatusChanged;

    public ElementTheme AppTheme => ThemeService.ForApp(Settings.Read<GlobalSettings>(GlobalSettings.Section).Theme);

    public async Task StartAsync(string[] args)
    {
        Directory.CreateDirectory(DataDirectory);
        Serilog.Log.Logger = LoggingSetup.Create(LogDirectory);
        Log = Serilog.Log.Logger.ForContext<AppHost>();
        Log.Information("FluxKit {Version} starting on {Os} ({Arch})", Version, Diagnostics.OsDescription, System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture);

        Settings = new SettingsService(SettingsPath, Serilog.Log.Logger);
        GlobalSettings global = Settings.Read<GlobalSettings>(GlobalSettings.Section);
        LoggingSetup.Apply(global, DateTimeOffset.UtcNow);
        Settings.SettingsChanged += OnSettingsChanged;

        VirtualDesktopBackend? forced = Environment.GetEnvironmentVariable("FLUXKIT_VD_BACKEND")?.ToUpperInvariant() switch
        {
            "KEYBOARD" => VirtualDesktopBackend.Keyboard,
            _ => null,
        };
        Desktops = await VirtualDesktopService.CreateAsync(Serilog.Log.Logger, forced);

        _selector = new SelectorWindow(Desktops, Serilog.Log.Logger);
        _selector.ApplyTheme(ResolveOverlayTheme());

        DragToSwitch = new DragToSwitchUtility(
            Settings,
            Desktops,
            () => _selector.View,
            () => new LowLevelInputHook(Serilog.Log.Logger),
            new Win32WindowProbe(),
            TimeProvider.System,
            Serilog.Log.Logger);
        DragToSwitch.StateChanged += (_, _) => _dispatcher.TryEnqueue(OnStatusChanged);

        Registry = new UtilityRegistry(
            id => Settings.Read<UtilityToggle>("utilities/" + id).Enabled,
            (id, enabled) => Settings.UpdateAsync<UtilityToggle>("utilities/" + id, t =>
            {
                t.Enabled = enabled;
                return t;
            }),
            Serilog.Log.Logger);
        Registry.Register(DragToSwitch);
        await Registry.StartEnabledAsync(CancellationToken.None);

        CreateTray();
        AppInstance.GetCurrent().Activated += (_, _) => _dispatcher.TryEnqueue(() => ShowMainWindow());

        bool background = args.Contains("--background", StringComparer.OrdinalIgnoreCase);
        if (!global.FirstRunCompleted || !background)
        {
            ShowMainWindow(args.Contains("--settings", StringComparer.OrdinalIgnoreCase) ? DragToSwitchUtility.Id : null);
        }

        if (args.Contains("--preview-selector", StringComparer.OrdinalIgnoreCase))
        {
            // Queued so it runs after startup completes, the same path a real drag takes.
            _dispatcher.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                () => _selector?.ShowPreview(Settings.Read<DragToSwitchSettings>(DragToSwitchSettings.Section)));
        }

        Log.Information("FluxKit ready (background={Background})", background);
    }

    public void ShowMainWindow(string? page = null)
    {
        if (_exiting)
        {
            return;
        }

        if (_mainWindow is null)
        {
            _mainWindow = new MainWindow();
            _mainWindow.Closed += (_, _) => _mainWindow = null;
        }

        _mainWindow.Activate();
        _mainWindow.BringToFront();
        if (page is not null)
        {
            _mainWindow.NavigateTo(page);
        }
    }

    public async Task SetUtilityEnabledAsync(string id, bool enabled)
    {
        await Registry.SetEnabledAsync(id, enabled);
        OnStatusChanged();
    }

    public async Task ExitAsync()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        Log.Information("FluxKit exiting");
        try
        {
            await Registry.DisposeAsync();
            Desktops.Dispose();
            await Settings.FlushAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during shutdown");
        }

        _tray?.Dispose();
        _mainWindow?.Close();
        _selector?.Close();
        await Serilog.Log.CloseAndFlushAsync();
        Application.Current.Exit();
    }

    public ElementTheme ResolveOverlayTheme() =>
        ThemeService.ForOverlay(Settings.Read<DragToSwitchSettings>(DragToSwitchSettings.Section).OverlayTheme, AppTheme);
    private void CreateTray()
    {
        _tray = new TrayIcon(TrayMenu.IconPath(Registry), TrayMenu.Tooltip(Registry));
        _tray.Activated += (_, _) => ShowMainWindow();
        _tray.TaskbarRecreated += async (_, _) =>
        {
            Log.Information("Explorer restarted; re-initializing shell integration");
            await Desktops.ReinitializeAsync();
            _selector?.RePin();
            OnStatusChanged();
        };
        _tray.MenuProvider = () => TrayMenu.Build(this);
    }

    private void OnStatusChanged()
    {
        _tray?.SetIcon(TrayMenu.IconPath(Registry));
        _tray?.SetTooltip(TrayMenu.Tooltip(Registry));
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSettingsChanged(object? sender, string section)
    {
        _dispatcher.TryEnqueue(() =>
        {
            if (section is GlobalSettings.Section or "*")
            {
                LoggingSetup.Apply(Settings.Read<GlobalSettings>(GlobalSettings.Section), DateTimeOffset.UtcNow);
                ThemeChanged?.Invoke(this, EventArgs.Empty);
            }

            _selector?.ApplyTheme(ResolveOverlayTheme());
        });
    }

    public static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }
}
