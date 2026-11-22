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
    private const double EdgeInset = 6;
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
    private HaloWindow? _halo;
    private readonly FluxKit.Platform.Windows.WindowThumbnail _peek = new();
    private FluxKit.Platform.Watchers.WindowMoveWatcher? _moves;
    private FluxKit.Platform.Watchers.WindowMove? _dragged;
    private DispatcherQueueTimer? _dragTimer;
    private readonly Dictionary<ActivityKind, DropletWindow> _droplets = [];
    private PixelRect _placement;
    private (Windows.UI.Color Accent, float Strength) _haloGlow;
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
        Pill.HaloChanged += (_, glow) => ShowHalo(glow.Accent, glow.Strength);
        Pill.PeekChanged += (_, peek) => ShowPeek(peek.Window, peek.Area);
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
            Pill.ShowHint(Strings.Get(key), TourStep, (step, steps.Length));
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
        ShowDroplets(scene);
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
            ShowDroplets(null);
            return;
        }

        bool settingChanged = _monitorSetting != scene.Settings.Monitor;
        _monitorSetting = scene.Settings.Monitor;
        Pill.Render(scene);
        ShowDroplets(scene);
        UpdateStashWatcher(scene.Settings.WindowStash);
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
        _halo?.Close();
        _halo = null;
        _peek.Dispose();
        UpdateStashWatcher(false);
        ShowDroplets(null);
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
        WindowHelpers.RemoveBorder(_hwnd);
        if (!_pinned)
        {
            _ = PinAsync();
        }

        ShowHalo(_haloGlow.Accent, _haloGlow.Strength);
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
        _halo?.Hide();
        _peek.Place(default, visible: false);
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
            WindowHelpers.RemoveBorder(_hwnd);
            _halo?.Hide();
            ShowHalo(_haloGlow.Accent, _haloGlow.Strength);
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
        _placement = placement;
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

    /// <summary>
    /// Window stash (plan D): watches window drags while the setting is on. During a drag the pointer is checked a few
    /// times a second (only then); over the island the pill shows its lip, and letting go there tucks the window away.
    /// </summary>
    private void UpdateStashWatcher(bool on)
    {
        if (on == (_moves is not null))
        {
            return;
        }

        if (!on)
        {
            _moves!.Dispose();
            _moves = null;
            _dragTimer?.Stop();
            return;
        }

        _moves = new FluxKit.Platform.Watchers.WindowMoveWatcher(_log);
        _moves.Started += (_, move) => DispatcherQueue.TryEnqueue(() =>
        {
            _dragged = move;
            _dragTimer ??= CreateDragTimer();
            _dragTimer.Start();
        });
        _moves.Ended += (_, move) => DispatcherQueue.TryEnqueue(() => EndWindowDrag(move));
        _moves.Start();
    }

    private DispatcherQueueTimer CreateDragTimer()
    {
        DispatcherQueueTimer timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(70);
        timer.Tick += (_, _) => Pill.SetStashLip(_dragged is not null && PointerOverIsland());
        return timer;
    }

    private void EndWindowDrag(FluxKit.Platform.Watchers.WindowMove move)
    {
        _dragTimer?.Stop();
        _dragged = null;
        bool over = PointerOverIsland();
        Pill.SetStashLip(false);
        if (over && _visible && FluxKit.Platform.Windows.WindowThumbnail.Exists(move.Window))
        {
            string title = FluxKit.Platform.Windows.WindowThumbnail.TitleOf(move.Window);
            FluxKit.Platform.Windows.WindowThumbnail.TuckAway(move.Window, move.Before);
            _commands?.StashWindow(move.Window, title.Length > 0 ? title : Strings.Get("Island_StashUntitled"));
        }
    }

    /// <summary>Whether the pointer is over the island (with a little room around it), in screen pixels.</summary>
    private bool PointerOverIsland()
    {
        if (!_visible)
        {
            return false;
        }

        (int x, int y) = WindowHelpers.GetCursorPosition();
        Rect region = Pill.RegionBounds;
        double slack = 18 * _scale;
        return x >= _placement.Left + (region.Left * _scale) - slack && x <= _placement.Left + (region.Right * _scale) + slack
            && y >= _placement.Top && y <= _placement.Top + (region.Bottom * _scale) + slack;
    }

    /// <summary>
    /// Shows the watched window's live view (a DWM thumbnail) over the Peek page's frame, or hides it (plan C).
    /// <paramref name="area"/> is in view pixels of this window.
    /// </summary>
    private void ShowPeek(nint window, Rect? area)
    {
        if (window == 0)
        {
            _peek.Detach();
            return;
        }

        if (_peek.Source != window && !_peek.Attach(_hwnd, window))
        {
            return;
        }

        PixelRect pixels = area is { } a
            ? new PixelRect((int)(a.Left * _scale), (int)(a.Top * _scale), (int)(a.Right * _scale), (int)(a.Bottom * _scale))
            : default;
        _peek.Place(pixels, visible: area is not null && _visible);
    }

    /// <summary>
    /// Keeps one droplet window per torn-off activity (plan A): new ones pop in, docked ones close. Null closes them
    /// all, e.g. while the island hides for a full-screen app.
    /// </summary>
    private void ShowDroplets(IslandScene? scene)
    {
        IReadOnlyList<DropletPlace> places = scene?.Droplets ?? [];
        foreach (ActivityKind gone in _droplets.Keys.Where(kind => places.All(p => p.Kind != kind)).ToList())
        {
            _droplets[gone].Close();
            _droplets.Remove(gone);
        }

        foreach (DropletPlace place in places)
        {
            if (!_droplets.TryGetValue(place.Kind, out DropletWindow? droplet))
            {
                droplet = new DropletWindow(place.Kind, () => Pill.Commands, () => ((_placement.Left + _placement.Right) / 2, _placement.Top + (int)(48 * _scale)));
                _droplets[place.Kind] = droplet;
            }

            droplet.Show(scene!, place, Pill.Springs);
        }
    }

    /// <summary>
    /// Spreads the glow onto the desktop under the island (the light halo, ADR-015). The halo window is made the first
    /// time it is needed, sits just under the island, and moves with it between screens.
    /// </summary>
    private void ShowHalo(Windows.UI.Color accent, float strength)
    {
        _haloGlow = (accent, strength);
        if (_halo is null && strength <= 0)
        {
            return;
        }

        if (!_visible || _closed)
        {
            return;
        }

        _halo ??= new HaloWindow();
        int centreX = (_placement.Left + _placement.Right) / 2;
        _halo.Show(_hwnd, centreX, _placement.Top, _scale, accent, strength, springs: true);
    }

    private void ApplyRegion(Rect bounds)
    {
        _region = bounds;
        // Keep the region a few pixels inside the window on every side: at the very edge a frame line shows.
        int inset = (int)Math.Ceiling(EdgeInset * _scale);
        int maxRight = (int)Math.Floor(IslandPill.StageWidth * _scale) - inset;
        int maxBottom = (int)Math.Floor(IslandPill.StageHeight * _scale) - inset;
        var pixels = new PixelRect(
            Math.Max(inset, (int)Math.Floor((bounds.Left - ShadowSide) * _scale)),
            (int)Math.Floor(bounds.Top * _scale),
            Math.Min(maxRight, (int)Math.Ceiling((bounds.Right + ShadowSide) * _scale)),
            Math.Min(maxBottom, (int)Math.Ceiling((bounds.Bottom + ShadowBottom) * _scale)));
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
