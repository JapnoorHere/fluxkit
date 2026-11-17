using FluxKit.App.Common;
using FluxKit.App.Theming;
using FluxKit.Platform.VirtualDesktop;
using FluxKit.Platform.Windows;
using FluxKit.Tools.FluxIsland;
using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Serilog;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI.ViewManagement;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// The window that holds the island. It never moves or resizes while shown: the pill morphs inside it, and the
/// window region follows the pill so clicks anywhere else go to the app below. It is pinned to every desktop.
/// </summary>
public sealed partial class IslandWindow : Window
{
    // Room around the pill for its shadow, in DIPs. The shadow falls downwards, so the bottom gets more.
    private const double ShadowSide = PillShape.Bleed;
    private const double ShadowBottom = PillShape.Bleed + 4;
    private static readonly TimeSpan ShrinkDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan PreviewHold = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan TourStep = TimeSpan.FromSeconds(3.6);

    private readonly IVirtualDesktopService _desktops;
    private readonly ILogger _log;
    private readonly nint _hwnd;
    private readonly UISettings _uiSettings = new();
    private readonly AccessibilitySettings _accessibility = new();
    private readonly DispatcherQueueTimer _shrinkTimer;
    private Rect _region;
    private double _scale = 1;
    private bool _visible;
    private bool _pinned;
    private bool _closed;
    private readonly IIslandCommands? _commands;
    private long _previewUntil;
    private DispatcherQueueTimer? _tourTimer;
    private nint _focusReturn;
    private PixelRect _monitor;
    private IslandMonitor _monitorSetting;
    private IslandActivity? _lastPrimary;

    public IslandWindow(IIslandCommands commands, IVirtualDesktopService desktops, ILogger log)
    {
        _desktops = desktops;
        _log = log.ForContext<IslandWindow>();
        InitializeComponent();
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        AppWindow.IsShownInSwitchers = false;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        SystemBackdrop = new TransparentBackdrop();
        WindowHelpers.MakeOverlay(_hwnd);
        WindowHelpers.RemoveFrame(_hwnd);

        _commands = commands;
        Pill.Commands = commands;
        Pill.TargetChanged += OnTargetChanged;
        Pill.HintSeen += (_, hint) => HintSeen?.Invoke(this, hint);
        Pill.ShareRequested += (_, files) => ShareSheet.Show(_hwnd, files, Strings.Get("Island_ShareTitle"));
        Pill.KeyboardReleased += (_, _) =>
        {
            WindowHelpers.ReleaseFocus(_hwnd, _focusReturn);
            _focusReturn = 0;
        };
        Activated += (_, e) =>
        {
            // Clicking into another window closes a panel the keyboard opened, as a menu would.
            if (e.WindowActivationState == WindowActivationState.Deactivated && Pill.IsKeyboardOpen)
            {
                Pill.CloseKeyboardPanel();
            }
        };
        _shrinkTimer = DispatcherQueue.CreateTimer();
        _shrinkTimer.Interval = ShrinkDelay;
        _shrinkTimer.IsRepeating = false;
        _shrinkTimer.Tick += (t, _) =>
        {
            t.Stop();
            ApplyRegion(Pill.RegionBounds);
        };
        View = new IslandViewAdapter(this);
    }

    /// <summary>Thread-safe view used by the tool.</summary>
    public IIslandView View { get; }

    /// <summary>The shortcut: opens the panel with keyboard focus, or closes it.</summary>
    public void TogglePanel()
    {
        if (_closed || !_visible || _previewUntil != 0)
        {
            return;
        }

        Pill.ToggleFromKeyboard();
        if (Pill.IsKeyboardOpen && _focusReturn == 0)
        {
            _focusReturn = WindowHelpers.TakeFocus(_hwnd);
        }
    }

    /// <summary>Pins again after Explorer restarts.</summary>
    public void RePin()
    {
        _pinned = false;
        if (_visible)
        {
            _ = PinAsync();
        }
    }
    /// <summary>A one-time tip was shown on the island; the host saves it so it never shows again.</summary>
    public event EventHandler<IslandHints>? HintSeen;

    /// <summary>
    /// The first-run tour: a short walk through what the island does, played on the island itself with a caption
    /// under it. About half a minute; the live island comes back when it ends.
    /// </summary>
    public void PlayTour()
    {
        if (_closed)
        {
            return;
        }

        (string Sample, bool Open, string Key)[] steps =
        [
            ("clock", false, "Island_Tour1"),
            ("music", false, "Island_Tour2"),
            ("music", true, "Island_Tour3"),
            ("split", false, "Island_Tour4"),
            ("copied", false, "Island_Tour5"),
            ("command", false, "Island_Tour6"),
            ("drop", false, "Island_Tour7"),
            ("glance", false, "Island_Tour8"),
        ];
        int step = 0;
        _tourTimer?.Stop();
        _tourTimer = DispatcherQueue.CreateTimer();
        _tourTimer.Interval = TourStep;
        DispatcherQueueTimer timer = _tourTimer;
        void Next()
        {
            if (step >= steps.Length)
            {
                timer.Stop();
                Pill.HideHint();
                _previewUntil = Environment.TickCount64; // The next live update takes over again.
                return;
            }

            (string sample, bool open, string key) = steps[step++];
            ShowPreview(sample, open);
            Pill.ShowHint(Strings.Get(key), TourStep);
        }

        timer.Tick += (_, _) => Next();
        Next();
        timer.Start();
        _log.Information("Island tour started");
    }


    /// <summary>
    /// Shows one of the <see cref="IslandSamples"/> for <see cref="PreviewHold"/>, even while live updates arrive
    /// (music keeps sending them). Buttons do nothing meanwhile; afterwards the live island and its commands return.
    /// </summary>
    public void ShowPreview(string sample, bool open)
    {
        IslandScene scene = IslandSamples.Create(sample, DateTimeOffset.Now);
        _previewUntil = 0;
        Pill.Commands = null; // Before the first render, so nothing in a preview acts or shows one-time tips.
        RenderCore(scene);
        _previewUntil = Environment.TickCount64 + (long)PreviewHold.TotalMilliseconds;
        Pill.Preview(scene, open, dropping: sample == IslandSamples.Drop, sample: sample);
        _log.Information("Island preview {Sample} (open={Open})", sample, open);
    }

    internal void RenderCore(IslandScene scene)
    {
        if (_closed)
        {
            return;
        }

        if (_previewUntil != 0)
        {
            if (Environment.TickCount64 < _previewUntil)
            {
                return;
            }

            _previewUntil = 0;
            Pill.Commands = _commands;
        }

        Pill.Springs = scene.Settings.Effects == IslandEffects.Full && _uiSettings.AnimationsEnabled;
        Pill.HighContrast = _accessibility.HighContrast;
        if (scene.Hidden)
        {
            HideCore();
            return;
        }

        bool settingChanged = _monitorSetting != scene.Settings.Monitor;
        _monitorSetting = scene.Settings.Monitor;
        Pill.Render(scene);
        if (!_visible)
        {
            ShowCore();
        }
        else if (settingChanged)
        {
            MoveToChosenMonitor();
        }
        else if (_monitorSetting == IslandMonitor.FollowMouse && scene.Primary is { } primary && !ReferenceEquals(primary, _lastPrimary) && !Pill.IsKeyboardOpen)
        {
            MoveToChosenMonitor();
        }

        _lastPrimary = scene.Primary;
    }

    internal void CloseCore()
    {
        if (_closed)
        {
            return;
        }

        HideCore();
        _closed = true;
        _shrinkTimer.Stop();
        Close();
    }

    private void ShowCore()
    {
        PixelRect placement = Place();
        _visible = true;
        Pill.SetActive(true);
        Pill.SnapToTarget();
        ApplyRegion(Pill.RegionBounds);
        WindowHelpers.ShowNoActivate(_hwnd, placement);
        if (!_pinned)
        {
            _ = PinAsync();
        }

        _log.Debug("Island shown at {Placement} (scale {Scale})", placement, _scale);
    }

    private void HideCore()
    {
        if (!_visible)
        {
            return;
        }

        _visible = false;
        Pill.SetActive(false);
        WindowHelpers.Hide(_hwnd);
    }

    /// <summary>
    /// Moves the island if it is on the wrong screen: after the setting changes, or (following the mouse) when
    /// something new shows while the pointer is on another screen.
    /// </summary>
    private void MoveToChosenMonitor()
    {
        (PixelRect monitor, _) = _monitorSetting == IslandMonitor.FollowMouse ? WindowHelpers.GetPointerMonitor() : WindowHelpers.GetPrimaryMonitor();
        if (monitor != _monitor)
        {
            PixelRect placement = Place();
            ApplyRegion(Pill.RegionBounds);
            WindowHelpers.ShowNoActivate(_hwnd, placement);
        }
    }

    /// <summary>Top centre of the chosen monitor (main, or where the pointer is), over the top edge of whatever is there.</summary>
    private PixelRect Place()
    {
        (PixelRect monitor, double scale) = _monitorSetting == IslandMonitor.FollowMouse ? WindowHelpers.GetPointerMonitor() : WindowHelpers.GetPrimaryMonitor();
        _monitor = monitor;
        _scale = scale;
        int width = (int)Math.Ceiling(IslandPill.StageWidth * scale);
        int height = (int)Math.Ceiling(IslandPill.StageHeight * scale);
        int left = monitor.Left + ((monitor.Width - width) / 2);
        var placement = new PixelRect(left, monitor.Top, left + width, monitor.Top + height);
        AppWindow.MoveAndResize(new RectInt32(placement.Left, placement.Top, width, height));
        return placement;
    }

    /// <summary>
    /// A growing pill gets its larger region at once; a shrinking one keeps the old region until the spring has
    /// settled, so the shape is never cut off mid-animation.
    /// </summary>
    private void OnTargetChanged(object? sender, Rect target)
    {
        if (!_visible)
        {
            return;
        }

        Rect union = _region;
        union.Union(target);
        ApplyRegion(union);
        _shrinkTimer.Stop();
        _shrinkTimer.Start();
    }

    private void ApplyRegion(Rect bounds)
    {
        _region = bounds;
        var pixels = new PixelRect(
            (int)Math.Floor((bounds.Left - ShadowSide) * _scale),
            (int)Math.Floor(bounds.Top * _scale),
            (int)Math.Ceiling((bounds.Right + ShadowSide) * _scale),
            (int)Math.Ceiling((bounds.Bottom + ShadowBottom) * _scale));
        int radius = (int)Math.Ceiling((Math.Min(bounds.Height / 2, PillShape.MaxCornerRadius) + ShadowSide) * _scale);
        WindowHelpers.SetHitRegion(_hwnd, pixels, radius);
    }

    private async Task PinAsync()
    {
        try
        {
            _pinned = await _desktops.PinWindowAsync(_hwnd);
            _log.Debug("Island pinned to all desktops: {Pinned}", _pinned);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Pinning the island to all desktops failed");
        }
    }
}
