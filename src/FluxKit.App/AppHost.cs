using System.Reflection;
using FluxKit.App.Modules.DragToSwitch;
using FluxKit.App.Modules.FluxIsland;
using FluxKit.App.Shell;
using FluxKit.App.Theming;
using FluxKit.Core;
using FluxKit.Core.Logging;
using FluxKit.Core.Settings;
using FluxKit.Core.Tools;
using FluxKit.Platform.Input;
using FluxKit.Platform.Shell;
using FluxKit.Platform.Startup;
using FluxKit.Platform.VirtualDesktop;
using FluxKit.Tools.DragToSwitch;
using FluxKit.Tools.DragToSwitch.Interaction;
using FluxKit.Tools.FluxIsland;
using FluxKit.Tools.FluxIsland.Sources;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Serilog;

namespace FluxKit.App;

/// <summary>
/// Composition root and application lifecycle: settings, logging, theme, virtual desktops, tools,
/// tray icon and windows. Pages reach shared services through <see cref="Current"/>.
/// </summary>
public sealed class AppHost
{
    private readonly DispatcherQueue _dispatcher;
    private MainWindow? _mainWindow;
    private SelectorWindow? _selector;
    private IslandWindow? _island;
    private TrayIcon? _tray;
    private bool _exiting;

    public AppHost(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
        Current = this;
        DataDirectory = FluxKitPaths.Root;
        LogDirectory = FluxKitPaths.LogsFolder;
        SettingsPath = FluxKitPaths.SettingsFile;
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

    public ToolRegistry Registry { get; private set; } = null!;

    public DragToSwitchTool DragToSwitch { get; private set; } = null!;

    public FluxIslandTool FluxIsland { get; private set; } = null!;

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

        DragToSwitch = new DragToSwitchTool(
            Settings,
            Desktops,
            () => _selector.View,
            () => new LowLevelInputHook(Serilog.Log.Logger),
            new Win32WindowProbe(),
            TimeProvider.System,
            Serilog.Log.Logger);
        DragToSwitch.StateChanged += (_, _) => _dispatcher.TryEnqueue(OnStatusChanged);

        Registry = new ToolRegistry(
            id => Settings.Read<ToolToggle>(SettingsKeys.Tool(id)).Enabled,
            (id, enabled) => Settings.UpdateAsync<ToolToggle>(SettingsKeys.Tool(id), t =>
            {
                t.Enabled = enabled;
                return t;
            }),
            Serilog.Log.Logger);
        Registry.Register(DragToSwitch);

        FluxIsland = new FluxIslandTool(
            Settings,
            commands =>
            {
                _island = new IslandWindow(commands, Desktops, Serilog.Log.Logger);
                return _island.View;
            },
            SystemSources.Create(Serilog.Log.Logger, Desktops),
            TimeProvider.System,
            Serilog.Log.Logger);
        FluxIsland.StateChanged += (_, _) => _dispatcher.TryEnqueue(OnStatusChanged);
        Registry.Register(FluxIsland);
        await Registry.StartEnabledAsync(CancellationToken.None);

        CreateTray();
        AppInstance.GetCurrent().Activated += (_, _) => _dispatcher.TryEnqueue(() => ShowMainWindow());

        bool background = args.Contains("--background", StringComparer.OrdinalIgnoreCase);
        if (!global.FirstRunCompleted || !background)
        {
            int page = Array.FindIndex(args, a => a.Equals("--page", StringComparison.OrdinalIgnoreCase));
            ShowMainWindow(page >= 0 && page + 1 < args.Length
                ? args[page + 1]
                : args.Contains("--settings", StringComparer.OrdinalIgnoreCase) ? DragToSwitchTool.Id : null);
        }

        if (args.Contains("--preview-selector", StringComparer.OrdinalIgnoreCase))
        {
            // Queued so it runs after startup completes, the same path a real drag takes.
            _dispatcher.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                () => _selector?.ShowPreview(Settings.Read<DragToSwitchSettings>(DragToSwitchSettings.Section)));
        }

        int islandPreview = Array.FindIndex(args, a => a.Equals("--preview-island", StringComparison.OrdinalIgnoreCase));
        if (islandPreview >= 0 && islandPreview + 1 < args.Length)
        {
            // "music" shows the compact pill, "music-open" the open panel.
            string sample = args[islandPreview + 1];
            bool open = sample.EndsWith("-open", StringComparison.OrdinalIgnoreCase);
            _dispatcher.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                () => _island?.ShowPreview(open ? sample[..^"-open".Length] : sample, open));
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

    public async Task SetToolEnabledAsync(string id, bool enabled)
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
            _island?.RePin();
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
