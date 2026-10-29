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
    private const double ShadowSide = 10;
    private const double ShadowBottom = 14;
    private static readonly TimeSpan ShrinkDelay = TimeSpan.FromMilliseconds(500);

    private readonly IVirtualDesktopService _desktops;
    private readonly ILogger _log;
    private readonly nint _hwnd;
    private readonly UISettings _uiSettings = new();
    private readonly DispatcherQueueTimer _shrinkTimer;
    private Rect _region;
    private double _scale = 1;
    private bool _visible;
    private bool _pinned;
    private bool _closed;

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

        Pill.Commands = commands;
        Pill.TargetChanged += OnTargetChanged;
        _shrinkTimer = DispatcherQueue.CreateTimer();
        _shrinkTimer.Interval = ShrinkDelay;
        _shrinkTimer.IsRepeating = false;
        _shrinkTimer.Tick += (t, _) =>
        {
            t.Stop();
            ApplyRegion(Pill.TargetBounds);
        };
        View = new IslandViewAdapter(this);
    }

    /// <summary>Thread-safe view used by the tool.</summary>
    public IIslandView View { get; }

    /// <summary>Pins again after Explorer restarts.</summary>
    public void RePin()
    {
        _pinned = false;
        if (_visible)
        {
            _ = PinAsync();
        }
    }

    /// <summary>Shows one of the <see cref="IslandSamples"/> until the tool next has something to show. Never runs commands.</summary>
    public void ShowPreview(string sample, bool open)
    {
        IslandScene scene = IslandSamples.Create(sample, DateTimeOffset.Now);
        RenderCore(scene);
        Pill.Commands = null;
        Pill.Preview(scene, open, dropping: sample == IslandSamples.Drop);
        _log.Information("Island preview {Sample} (open={Open})", sample, open);
    }

    internal void RenderCore(IslandScene scene)
    {
        if (_closed)
        {
            return;
        }

        Pill.Springs = scene.Settings.Effects == IslandEffects.Full && _uiSettings.AnimationsEnabled;
        if (scene.Hidden)
        {
            HideCore();
            return;
        }

        Pill.Render(scene);
        if (!_visible)
        {
            ShowCore();
        }
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
        ApplyRegion(Pill.TargetBounds);
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

    /// <summary>Top centre of the primary monitor, over the top edge of whatever is there.</summary>
    private PixelRect Place()
    {
        (PixelRect monitor, double scale) = WindowHelpers.GetPrimaryMonitor();
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
