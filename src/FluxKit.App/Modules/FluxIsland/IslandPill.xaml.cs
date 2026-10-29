using System.Numerics;
using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// The island pill and its states: compact (what is happening now), peek (a second line after a short hover) and
/// the open panel (after a click). It wakes only when something changes: once a minute for the clock, once a second
/// while a timer counts down on screen, and for hover and clicks.
/// </summary>
public sealed partial class IslandPill : UserControl
{
    public const double StageWidth = 440;
    public const double StageHeight = 212;
    public const double TopInset = 6;

    private const double CompactHeight = 36;
    private const double MinCompactWidth = 220;
    private const double MaxCompactWidth = 300;
    private const float HoverScale = 1.04f;
    private const float PressScale = 0.94f;
    private static readonly Size IdleSize = new(120, 32);
    private static readonly Size BareSize = new(88, 28);
    private static readonly Size PeekSize = new(320, 52);
    private static readonly Size PanelSize = new(400, 156);
    private static readonly Size MediaPanelSize = new(400, 172);
    private static readonly TimeSpan PeekDelay = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan CloseDelay = TimeSpan.FromMilliseconds(600);

    private readonly PillShape _shape;
    private readonly AlbumArt _art = new();
    private readonly DispatcherQueueTimer _peekTimer;
    private readonly DispatcherQueueTimer _closeTimer;
    private readonly DispatcherQueueTimer _clockTimer;
    private readonly DispatcherQueueTimer _tickTimer;
    private IslandScene _scene = IslandScene.Empty;
    private PillMode _mode;
    private UIElement? _shownView;
    private string? _shownKey;
    private bool _hovering;
    private bool _pressed;
    private bool _active;

    public IslandPill()
    {
        InitializeComponent();
        Foreground = new SolidColorBrush(IslandPalette.TextPrimary);
        var secondary = new SolidColorBrush(IslandPalette.TextSecondary);
        CompactSubtitle.Foreground = secondary;
        PanelDate.Foreground = secondary;
        PanelHint.Foreground = secondary;
        PanelBatteryText.Foreground = secondary;
        Stage.Width = StageWidth;
        Stage.Height = StageHeight;
        Surface.Margin = new Thickness(0, TopInset, 0, 0);
        _shape = new PillShape(ShapeHost, ContentLayer, Stage, new Vector2((float)StageWidth, (float)StageHeight), (float)TopInset);
        MediaView.UseArt(_art);
        _art.Changed += (_, _) =>
        {
            CompactArt.Source = _art.Image;
            if (_scene.Media is not null)
            {
                _shownKey = null;
                Update();
            }
        };

        _peekTimer = OneShot(PeekDelay, () => SetMode(PillMode.Peek));
        _closeTimer = OneShot(CloseDelay, () => SetMode(PillMode.Compact));
        _clockTimer = OneShot(TimeSpan.FromMinutes(1), OnClockTick);
        _tickTimer = DispatcherQueue.CreateTimer();
        _tickTimer.Interval = TimeSpan.FromSeconds(1);
        _tickTimer.Tick += (_, _) => Update();

        Surface.PointerEntered += OnPointerEntered;
        Surface.PointerExited += OnPointerExited;
        Surface.PointerPressed += OnPointerPressed;
        Surface.PointerReleased += OnPointerReleased;
        Surface.PointerCaptureLost += (_, _) => Unpress();
        Surface.PointerCanceled += (_, _) => Unpress();
    }

    private enum PillMode
    {
        Compact,
        Peek,
        Panel,
    }

    /// <summary>What buttons on the island call. Unset in previews, where nothing should happen.</summary>
    public IIslandCommands? Commands
    {
        get => MediaView.Commands;
        set => MediaView.Commands = value;
    }

    /// <summary>False when Windows animations are off or the tool uses Light effects; springs become short fades.</summary>
    public bool Springs { get; set; } = true;

    /// <summary>The pill's resting bounds in stage DIPs. The window keeps its click region in step with it.</summary>
    public Rect TargetBounds { get; private set; }

    public event EventHandler<Rect>? TargetChanged;

    public void Render(IslandScene scene)
    {
        _scene = scene;
        _art.Set(scene.Media?.Thumbnail ?? ReadOnlyMemory<byte>.Empty);
        Update();
    }

    /// <summary>Shows <paramref name="scene"/>, opened as the panel when <paramref name="open"/> is true. For previews.</summary>
    public void Preview(IslandScene scene, bool open)
    {
        _mode = open ? PillMode.Panel : PillMode.Compact;
        Render(scene);
    }

    /// <summary>Starts or stops the clock and timers, e.g. while the window is hidden for a full-screen app.</summary>
    public void SetActive(bool active)
    {
        if (active == _active)
        {
            return;
        }

        _active = active;
        if (!active)
        {
            _clockTimer.Stop();
            _tickTimer.Stop();
            _peekTimer.Stop();
            _closeTimer.Stop();
            _mode = PillMode.Compact;
            _hovering = false;
            _shape.ScaleTo(1, springs: false);
            return;
        }

        Update();
    }

    /// <summary>Puts the shape straight at its current size, e.g. when the island comes back after being hidden.</summary>
    public void SnapToTarget() => _shape.Snap(new Vector2((float)TargetBounds.Width, (float)TargetBounds.Height));

    private void Update()
    {
        DateTimeOffset now = DateTimeOffset.Now;
        (UIElement view, string key, Size size) = _mode == PillMode.Panel ? BuildPanel(now) : BuildCompact(now);
        ShowView(view, key);
        MorphTo(size);
        ScheduleTimers(now);
    }

    private (UIElement View, string Key, Size Size) BuildCompact(DateTimeOffset now)
    {
        bool peek = _mode == PillMode.Peek;
        if (_scene.Primary is not { } primary)
        {
            if (!peek)
            {
                IdleClock.Text = IslandPresenter.FormatClock(now);
                IdleClock.Visibility = _scene.Settings.ShowClockWhenIdle ? Visibility.Visible : Visibility.Collapsed;
                AutomationProperties.SetName(Surface, IdleClock.Text);
                return (IdleClock, "idle", _scene.Settings.ShowClockWhenIdle ? IdleSize : BareSize);
            }

            // Peeking at an idle island shows the time and the date.
            return FillCompact(new CompactContent(string.Empty, IslandPresenter.FormatClock(now), null, IslandPresenter.FormatDate(now), IslandPalette.TextPrimary), "peek-idle", twoLines: false, media: null);
        }

        CompactContent content = IslandPresenter.Describe(primary, now);
        bool twoLines = peek && content.Subtitle is not null;
        return FillCompact(content, $"{primary.Kind}-{twoLines}", twoLines, primary.Kind == ActivityKind.Media ? primary.Media : null);
    }

    private (UIElement View, string Key, Size Size) FillCompact(CompactContent content, string key, bool twoLines, MediaInfo? media)
    {
        // Music shows its art instead of the note glyph, and the bouncing bars on the right.
        bool art = media is not null && _art.Image is not null;
        CompactArtFrame.Visibility = art ? Visibility.Visible : Visibility.Collapsed;
        CompactBars.Visibility = media is null ? Visibility.Collapsed : Visibility.Visible;
        if (media is not null)
        {
            CompactBars.SetColor(_art.Accent);
            CompactBars.SetPlaying(media.IsPlaying, Springs);
            content = content with { Accent = _art.Accent };
        }

        CompactGlyph.Glyph = content.Glyph;
        CompactGlyph.Visibility = content.Glyph.Length > 0 && !art ? Visibility.Visible : Visibility.Collapsed;
        CompactGlyph.Foreground = new SolidColorBrush(content.Accent);
        CompactTitle.Text = content.Title;
        CompactSubtitle.Text = content.Subtitle ?? string.Empty;
        CompactSubtitle.Visibility = twoLines ? Visibility.Visible : Visibility.Collapsed;
        CompactTrail.Text = content.Trail ?? string.Empty;
        CompactTrail.Foreground = new SolidColorBrush(content.Accent == IslandPalette.TextPrimary ? IslandPalette.TextSecondary : content.Accent);
        CompactTrail.Visibility = content.Trail is null ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetName(Surface, string.Join(", ", new[] { content.Title, content.Subtitle, content.Trail }.Where(t => !string.IsNullOrEmpty(t))));

        if (twoLines)
        {
            return (CompactView, key, PeekSize);
        }

        CompactView.Visibility = Visibility.Visible;
        CompactView.Measure(new Size(double.PositiveInfinity, CompactHeight));
        double width = Math.Clamp(Math.Ceiling(CompactView.DesiredSize.Width / 4) * 4, MinCompactWidth, MaxCompactWidth);
        return (CompactView, key, new Size(width, CompactHeight));
    }

    private (UIElement View, string Key, Size Size) BuildPanel(DateTimeOffset now)
    {
        if (_scene.Media is { } media)
        {
            MediaView.Animate = Springs;
            MediaView.Update(media, now);
            AutomationProperties.SetName(Surface, string.Join(", ", new[] { media.Title, media.Artist }.Where(t => !string.IsNullOrEmpty(t))));
            return (MediaView, "panel-media", MediaPanelSize);
        }

        PanelClock.Text = IslandPresenter.FormatClock(now);
        PanelDate.Text = IslandPresenter.FormatDate(now);
        PowerSnapshot power = _scene.Power;
        PanelBattery.Visibility = power.HasBattery ? Visibility.Visible : Visibility.Collapsed;
        if (power.HasBattery)
        {
            CompactContent battery = IslandPresenter.Describe(new IslandActivity(power.IsPluggedIn ? ActivityKind.PluggedIn : ActivityKind.Unplugged) { Value = power.Percent }, now);
            PanelBatteryGlyph.Glyph = battery.Glyph;
            PanelBatteryGlyph.Foreground = new SolidColorBrush(battery.Accent);
            PanelBatteryText.Text = battery.Trail;
        }

        AutomationProperties.SetName(Surface, $"{PanelClock.Text}, {PanelDate.Text}");
        return (PanelView, "panel-clock", PanelSize);
    }

    private void ShowView(UIElement view, string key)
    {
        if (key == _shownKey)
        {
            return;
        }

        UIElement? previous = _shownView;
        _shownView = view;
        _shownKey = key;
        if (previous is not null && previous != view)
        {
            IslandMotion.Exit(previous, () => _shownView != previous);
        }

        if (view == PanelView)
        {
            // Rows arrive one after the other.
            IslandMotion.Enter(PanelView, TimeSpan.Zero, Springs);
            IslandMotion.Enter(PanelHeader, IslandMotion.Stagger, Springs);
            IslandMotion.Enter(PanelHint, IslandMotion.Stagger * 2, Springs);
        }
        else if (view == MediaView)
        {
            IslandMotion.Enter(MediaView, TimeSpan.Zero, Springs);
            UIElement[] rows = MediaView.Rows;
            for (int i = 0; i < rows.Length; i++)
            {
                IslandMotion.Enter(rows[i], IslandMotion.Stagger * (i + 1), Springs);
            }
        }
        else
        {
            IslandMotion.Enter(view, previous is null ? TimeSpan.Zero : IslandMotion.Stagger, Springs);
        }
    }

    private void MorphTo(Size size)
    {
        Surface.Width = size.Width;
        Surface.Height = size.Height;
        _shape.MorphTo(new Vector2((float)size.Width, (float)size.Height), Springs);
        var bounds = new Rect((StageWidth - size.Width) / 2, TopInset, size.Width, size.Height);
        if (bounds != TargetBounds)
        {
            TargetBounds = bounds;
            TargetChanged?.Invoke(this, bounds);
        }
    }

    private void ScheduleTimers(DateTimeOffset now)
    {
        if (!_active)
        {
            return;
        }

        // Wake just after the next minute starts, so the clock never shows a stale minute for long.
        _clockTimer.Stop();
        _clockTimer.Interval = TimeSpan.FromMilliseconds(((60 - now.Second) * 1000) - now.Millisecond + 50);
        _clockTimer.Start();

        // Tick once a second only while something on screen moves: a running timer, or the seek bar of playing music.
        bool counting = (_scene.Primary?.Kind == ActivityKind.Timer && _scene.Timer?.State == TimerState.Running)
            || (_mode == PillMode.Panel && _scene.Media?.IsPlaying == true);
        if (counting && !_tickTimer.IsRunning)
        {
            _tickTimer.Start();
        }
        else if (!counting && _tickTimer.IsRunning)
        {
            _tickTimer.Stop();
        }
    }

    private void OnClockTick() => Update();

    private void SetMode(PillMode mode)
    {
        if (mode == _mode)
        {
            return;
        }

        _mode = mode;
        if (mode == PillMode.Panel)
        {
            _shape.ScaleTo(1, Springs);
        }

        Update();
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _hovering = true;
        _closeTimer.Stop();
        if (_mode != PillMode.Panel)
        {
            _shape.ScaleTo(HoverScale, Springs);
            _peekTimer.Start();
        }
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _hovering = false;
        _pressed = false;
        _peekTimer.Stop();
        _shape.ScaleTo(1, Springs);
        if (_mode != PillMode.Compact)
        {
            _closeTimer.Start();
        }
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_mode == PillMode.Panel)
        {
            return;
        }

        _pressed = true;
        _shape.ScaleTo(PressScale, Springs);
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        _pressed = false;
        _peekTimer.Stop();
        SetMode(PillMode.Panel);
    }

    private void Unpress()
    {
        if (_pressed)
        {
            _pressed = false;
            _shape.ScaleTo(_hovering ? HoverScale : 1, Springs);
        }
    }

    private DispatcherQueueTimer OneShot(TimeSpan interval, Action action)
    {
        DispatcherQueueTimer timer = DispatcherQueue.CreateTimer();
        timer.Interval = interval;
        timer.IsRepeating = false;
        timer.Tick += (t, _) =>
        {
            t.Stop();
            action();
        };
        return timer;
    }
}
