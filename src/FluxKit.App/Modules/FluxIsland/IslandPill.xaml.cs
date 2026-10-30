using System.Numerics;
using System.Runtime.InteropServices;
using FluxKit.App.Common;
using FluxKit.Platform.Shell;
using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;

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
    private const double DotsRoom = 20;
    private const double RailRoom = 34;
    private const float HoverScale = 1.04f;
    private const float PressScale = 0.94f;
    private const int WheelNotch = 120;
    private const double BubbleSize = 36;
    private const double BubbleGap = 8;
    private static readonly Size IdleSize = new(120, 32);
    private static readonly Size BareSize = new(88, 28);
    private static readonly Size PeekSize = new(320, 52);
    private static readonly Size PanelSize = new(400, 172);
    private static readonly Size EmptyMediaSize = new(400, 140);
    private static readonly Size MediaPanelSize = new(400, 172);
    private static readonly Size ShelfPanelSize = new(400, 132);
    private static readonly TimeSpan PeekDelay = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan CloseDelay = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan DragLeaveDelay = TimeSpan.FromMilliseconds(80);
    private static readonly TimeSpan DropLingerDelay = TimeSpan.FromMilliseconds(1800);

    private readonly PillShape _shape;
    private readonly AlbumArt _art = new();
    private readonly DispatcherQueueTimer _peekTimer;
    private readonly DispatcherQueueTimer _closeTimer;
    private readonly DispatcherQueueTimer _clockTimer;
    private readonly DispatcherQueueTimer _tickTimer;
    private readonly DispatcherQueueTimer _dragLeaveTimer;
    private readonly DispatcherQueueTimer _lingerTimer;
    private IslandScene _scene = IslandScene.Empty;
    private bool _dragging;
    private bool _previewDrop;
    private bool _bubbleShown;
    private long _lastSwap;
    private int _wheel;
    private PillMode _mode;
    private PanelPage? _page;
    private UIElement? _shownView;
    private string? _shownKey;
    private bool _hovering;
    private bool _pressed;
    private bool _active;
    private string? _thumbPath;
    private ImageSource? _thumb;

    public IslandPill()
    {
        InitializeComponent();
        Foreground = new SolidColorBrush(IslandPalette.TextPrimary);
        var secondary = new SolidColorBrush(IslandPalette.TextSecondary);
        CompactSubtitle.Foreground = secondary;
        CompactBurst.Stroke = new SolidColorBrush(IslandPalette.Orange);
        CompactLevelFill.Background = new SolidColorBrush(IslandPalette.TextPrimary);
        BubbleFill.Fill = new SolidColorBrush(IslandPalette.Surface);
        BubbleEdge.Stroke = new SolidColorBrush(IslandPalette.Highlight);
        Bubble.Margin = new Thickness(0, TopInset + ((CompactHeight - BubbleSize) / 2), 0, 0);
        ElementCompositionPreview.SetIsTranslationEnabled(Bubble, true);
        Bubble.Tapped += (_, e) =>
        {
            e.Handled = true;
            Commands?.SwapActivities();
        };
        PressFeedback.Attach(Bubble);
        CompactAction.Click += (_, _) => RunAction(_scene.Primary);
        Surface.RightTapped += OnRightTapped;
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
        _dragLeaveTimer = OneShot(DragLeaveDelay, EndDrag);
        _lingerTimer = OneShot(DropLingerDelay, () =>
        {
            if (!_hovering)
            {
                SetMode(PillMode.Compact);
            }
        });

        MediaTab.Click += (_, _) => ShowPage(PanelPage.Media);
        TimerTab.Click += (_, _) => ShowPage(PanelPage.Timer);
        ShelfTab.Click += (_, _) => ShowPage(PanelPage.Shelf);
        PressFeedback.Attach(MediaTab);
        PressFeedback.Attach(TimerTab);
        PressFeedback.Attach(ShelfTab);

        Surface.PointerEntered += OnPointerEntered;
        Surface.PointerExited += OnPointerExited;
        Surface.PointerPressed += OnPointerPressed;
        Surface.PointerReleased += OnPointerReleased;
        Surface.PointerCaptureLost += (_, _) => Unpress();
        Surface.PointerCanceled += (_, _) => Unpress();
        Surface.PointerWheelChanged += OnWheel;
        Surface.DragEnter += OnDragEnter;
        Surface.DragOver += OnDragOver;
        Surface.DragLeave += OnDragLeave;
        Surface.Drop += OnDrop;
    }

    private enum PillMode
    {
        Compact,
        Peek,
        Panel,
    }

    private enum PanelPage
    {
        Media,
        Timer,
        Shelf,
    }

    /// <summary>What buttons on the island call. Unset in previews, where nothing should happen.</summary>
    public IIslandCommands? Commands
    {
        get => MediaView.Commands;
        set
        {
            MediaView.Commands = value;
            TimerView.Commands = value;
            ShelfView.Commands = value;
        }
    }

    /// <summary>False when Windows animations are off or the tool uses Light effects; springs become short fades.</summary>
    public bool Springs { get; set; } = true;

    /// <summary>The pill's resting bounds in stage DIPs. The window keeps its click region in step with it.</summary>
    public Rect TargetBounds { get; private set; }

    /// <summary>The pill plus the bubble, when it shows. The window's click region follows this.</summary>
    public Rect RegionBounds { get; private set; }

    public event EventHandler<Rect>? TargetChanged;

    public void Render(IslandScene scene)
    {
        if (_previewDrop)
        {
            _previewDrop = false;
            _dragging = false;
        }

        Show(scene);
    }

    /// <summary>Shows <paramref name="scene"/>, opened as the panel when <paramref name="open"/> is true. For previews.</summary>
    public void Preview(IslandScene scene, bool open, bool dropping = false)
    {
        _mode = open || dropping ? PillMode.Panel : PillMode.Compact;
        _dragging = dropping;
        _previewDrop = dropping;
        Show(scene);
    }

    private void Show(IslandScene scene)
    {
        _scene = scene;
        Surface.AllowDrop = scene.Settings.ShelfEnabled;
        _art.Set(scene.Media?.Thumbnail ?? ReadOnlyMemory<byte>.Empty);
        Update();
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
            _dragLeaveTimer.Stop();
            _lingerTimer.Stop();
            _mode = PillMode.Compact;
            _hovering = false;
            _dragging = false;
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
        if (ShowView(view, key) && view == CompactView && _scene.Primary is { IsAlert: true } alert)
        {
            // An alert arriving gets a little flourish: the charger ring sweeps, any other icon pops.
            if (alert.Kind == ActivityKind.PluggedIn)
            {
                CompactRing.Sweep(alert.Value, Springs);
            }
            else if (alert.Kind == ActivityKind.TimerDone)
            {
                // Time's up deserves more: a ring bursts out of the bell and the whole pill bounces once.
                IslandMotion.Pop(CompactGlyph, Springs);
                IslandMotion.Burst(CompactBurst, Springs);
                _shape.Bounce(Springs);
            }
            else
            {
                IslandMotion.Pop(CompactGlyph, Springs);
            }
        }

        UpdateBubble(view == CompactView && _mode == PillMode.Compact ? _scene.Secondary : null, size, now);
        MorphTo(size);
        ScheduleTimers(now);
    }

    /// <summary>Shows the second activity in a bubble that springs out from the pill's right edge, or tucks it away.</summary>
    private void UpdateBubble(IslandActivity? secondary, Size pill, DateTimeOffset now)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(Bubble);
        visual.CenterPoint = new Vector3((float)BubbleSize / 2, (float)BubbleSize / 2, 0);
        bool show = secondary is not null;
        float x = (float)((pill.Width / 2) + BubbleGap + (BubbleSize / 2));
        if (show)
        {
            CompactContent content = IslandPresenter.Describe(secondary!, now);
            bool art = secondary!.Kind == ActivityKind.Media && _art.Image is not null;
            BubbleArtBrush.ImageSource = art ? _art.Image : null;
            BubbleArt.Visibility = art ? Visibility.Visible : Visibility.Collapsed;
            BubbleGlyph.Visibility = art ? Visibility.Collapsed : Visibility.Visible;
            BubbleGlyph.Glyph = content.Glyph;
            BubbleGlyph.Foreground = new SolidColorBrush(secondary.Kind == ActivityKind.Media ? _art.Accent : content.Accent);
            AutomationProperties.SetName(Bubble, string.Join(", ", new[] { content.Title, content.Trail }.Where(t => !string.IsNullOrEmpty(t))));
            ToolTipService.SetToolTip(Bubble, content.Title);
        }

        if (show == _bubbleShown)
        {
            // Already out: just follow the pill's width.
            if (show)
            {
                Spring(visual, "Translation", new Vector3(x, 0, 0));
            }

            return;
        }

        _bubbleShown = show;
        if (show)
        {
            // It buds off the pill's edge: starts small and tucked in, then springs out to its place.
            Bubble.Visibility = Visibility.Visible;
            visual.StopAnimation("Opacity");
            visual.Opacity = 1;
            visual.Properties.InsertVector3("Translation", new Vector3(x - 24, 0, 0));
            visual.Scale = new Vector3(0.4f, 0.4f, 1);
            Spring(visual, "Translation", new Vector3(x, 0, 0));
            Spring(visual, "Scale", Vector3.One);
            return;
        }

        Spring(visual, "Translation", new Vector3(x - 24, 0, 0));
        Spring(visual, "Scale", new Vector3(0.4f, 0.4f, 1));
        IslandMotion.Exit(Bubble, () => !_bubbleShown);
    }

    private void Spring(Visual visual, string property, Vector3 to)
    {
        if (!Springs)
        {
            visual.StopAnimation(property);
            if (property == "Scale")
            {
                visual.Scale = to;
            }
            else
            {
                visual.Properties.InsertVector3(property, to);
            }

            return;
        }

        SpringVector3NaturalMotionAnimation spring = visual.Compositor.CreateSpringVector3Animation();
        spring.DampingRatio = 0.62f;
        spring.Period = TimeSpan.FromMilliseconds(50);
        spring.FinalValue = to;
        visual.StartAnimation(property, spring);
    }

    /// <summary>Runs the peek chip's action for <paramref name="activity"/> (see <see cref="IslandPresenter.ActionLabel"/>).</summary>
    private void RunAction(IslandActivity? activity)
    {
        switch (activity)
        {
            case { Kind: ActivityKind.MicMuted }:
                Commands?.ToggleMicMute();
                break;
            case { Kind: ActivityKind.DownloadDone, Path: { } download }:
                // Shown, never opened: a download could be a program, and running one should be the user's own click.
                ShellLauncher.ShowInFolder(download);
                Commands?.DismissAlert();
                break;
            case { Kind: ActivityKind.Screenshot, Path: { } screenshot }:
                ShellLauncher.Open(screenshot);
                Commands?.DismissAlert();
                break;
            case { Kind: ActivityKind.Copied, Path: { } link }:
                ShellLauncher.OpenLink(link);
                Commands?.DismissAlert();
                break;
        }
    }

    private async Task LoadThumbAsync(string path)
    {
        ImageSource? image = await FileThumbnail.LoadAsync(path, 48);
        if (path == _thumbPath && image is not null)
        {
            _thumb = image;
            _shownKey = null;
            Update();
        }
    }

    /// <summary>Right-click waves away the alert on screen, or closes the open panel.</summary>
    private void OnRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        e.Handled = true;
        if (_mode == PillMode.Panel)
        {
            SetMode(PillMode.Compact);
        }
        else if (_scene.Primary is { IsAlert: true })
        {
            Commands?.DismissAlert();
        }
    }

    private (UIElement View, string Key, Size Size) BuildCompact(DateTimeOffset now)
    {
        bool peek = _mode == PillMode.Peek;
        PanelRail.Visibility = Visibility.Collapsed;
        if (_scene.Primary is not { } primary)
        {
            if (!peek)
            {
                IdleClock.Text = IslandPresenter.FormatClock(now);
                IdleClock.Visibility = _scene.Settings.ShowClockWhenIdle ? Visibility.Visible : Visibility.Collapsed;
                AutomationProperties.SetName(Surface, IdleClock.Text);
                IdleDots.Update(_scene.Privacy, Springs);
                Size idle = _scene.Settings.ShowClockWhenIdle ? IdleSize : BareSize;
                return (IdleView, "idle", IdleDots.IsShowing ? new Size(idle.Width + DotsRoom, idle.Height) : idle);
            }

            // Peeking at an idle island shows the time and the date.
            return FillCompact(new CompactContent(string.Empty, IslandPresenter.FormatClock(now), null, IslandPresenter.FormatDate(now), IslandPalette.TextPrimary), "peek-idle", twoLines: false, activity: null);
        }

        CompactContent content = IslandPresenter.Describe(primary, now);
        bool twoLines = peek && content.Subtitle is not null;
        return FillCompact(content, $"{primary.Kind}-{twoLines}", twoLines, primary);
    }

    private (UIElement View, string Key, Size Size) FillCompact(CompactContent content, string key, bool twoLines, IslandActivity? activity)
    {
        // Music shows its art instead of the note glyph, and the bouncing bars on the right; the charger shows its ring.
        MediaInfo? media = activity?.Kind == ActivityKind.Media ? activity.Media : null;
        bool charging = activity?.Kind == ActivityKind.PluggedIn;
        string? thumbPath = activity?.Kind == ActivityKind.Screenshot ? activity.Path : null;
        if (thumbPath != _thumbPath)
        {
            _thumbPath = thumbPath;
            _thumb = null;
            if (thumbPath is not null)
            {
                _ = LoadThumbAsync(thumbPath);
            }
        }

        // A screenshot shows itself, small, where music shows its art.
        bool art = media is not null ? _art.Image is not null : _thumb is not null;
        CompactArt.Source = media is not null ? _art.Image : _thumb;
        CompactRing.Visibility = charging ? Visibility.Visible : Visibility.Collapsed;
        CompactDots.Update(_scene.Privacy, Springs);
        CompactArtFrame.Visibility = art ? Visibility.Visible : Visibility.Collapsed;
        CompactBars.Visibility = media is null ? Visibility.Collapsed : Visibility.Visible;
        if (media is not null)
        {
            CompactBars.SetColor(_art.Accent);
            CompactBars.SetPlaying(media.IsPlaying, Springs);
            content = content with { Accent = _art.Accent };
        }

        CompactGlyph.Glyph = content.Glyph;
        CompactGlyph.Visibility = content.Glyph.Length > 0 && !art && !charging ? Visibility.Visible : Visibility.Collapsed;
        CompactGlyph.Foreground = new SolidColorBrush(content.Accent);
        bool level = activity?.Kind is ActivityKind.Volume or ActivityKind.Brightness;
        CompactLevel.Visibility = level ? Visibility.Visible : Visibility.Collapsed;
        CompactTitle.Visibility = level ? Visibility.Collapsed : Visibility.Visible;
        if (level)
        {
            SetLevel(activity!.Value);
        }

        CompactTitle.Text = content.Title;
        CompactSubtitle.Text = content.Subtitle ?? string.Empty;
        CompactSubtitle.Visibility = twoLines ? Visibility.Visible : Visibility.Collapsed;
        CompactTrail.Text = content.Trail ?? string.Empty;
        CompactTrail.Foreground = new SolidColorBrush(content.Accent == IslandPalette.TextPrimary ? IslandPalette.TextSecondary : content.Accent);
        CompactTrail.Visibility = content.Trail is null ? Visibility.Collapsed : Visibility.Visible;
        string? action = _mode == PillMode.Peek && Commands is not null && activity is not null ? IslandPresenter.ActionLabel(activity) : null;
        CompactAction.Content = action;
        CompactAction.Visibility = action is null ? Visibility.Collapsed : Visibility.Visible;
        if (action is not null)
        {
            CompactTrail.Visibility = Visibility.Collapsed;
        }
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

    /// <summary>The level bar fills with a soft spring, so each wheel notch nudges it rather than jumping.</summary>
    private void SetLevel(int percent)
    {
        Visual fill = ElementCompositionPreview.GetElementVisual(CompactLevelFill);
        float scale = Math.Clamp(percent, 0, 100) / 100f;
        fill.CenterPoint = Vector3.Zero;
        if (!Springs)
        {
            fill.Scale = new Vector3(scale, 1, 1);
            return;
        }

        SpringVector3NaturalMotionAnimation spring = fill.Compositor.CreateSpringVector3Animation();
        spring.DampingRatio = 0.7f;
        spring.Period = TimeSpan.FromMilliseconds(40);
        spring.FinalValue = new Vector3(scale, 1, 1);
        fill.StartAnimation("Scale", spring);
    }

    /// <summary>The mouse wheel over the island changes the volume (the tool ignores it when that setting is off).</summary>
    private void OnWheel(object sender, PointerRoutedEventArgs e)
    {
        PointerPointProperties properties = e.GetCurrentPoint(Surface).Properties;
        bool sideways = properties.IsHorizontalMouseWheel || e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Shift);
        if (sideways)
        {
            // Tilt the wheel (or Shift+scroll) to swap the pill and the bubble.
            // A tilt sends a stream of events; one swap per gesture.
            long tick = Environment.TickCount64;
            if (_mode == PillMode.Compact && _scene.Secondary is not null && tick - _lastSwap > 450)
            {
                _lastSwap = tick;
                Commands?.SwapActivities();
            }

            e.Handled = true;
            return;
        }

        _wheel += properties.MouseWheelDelta;
        int notches = _wheel / WheelNotch;
        if (notches != 0)
        {
            _wheel -= notches * WheelNotch;
            Commands?.StepVolume(notches);
        }

        e.Handled = true;
    }

    private (UIElement View, string Key, Size Size) BuildPanel(DateTimeOffset now)
    {
        List<PanelPage> pages = AvailablePages();
        if (_page is not { } page || !pages.Contains(page))
        {
            page = DefaultPage(pages);
            _page = page;
        }

        ShowRail(pages, page);
        double inset = pages.Count > 1 ? RailRoom : 0;
        if (page == PanelPage.Media)
        {
            MediaView.SetInset(inset);
            MediaView.Animate = Springs;
            if (_scene.Media is not { } media)
            {
                MediaView.ShowEmpty();
                AutomationProperties.SetName(Surface, Strings.Get("Island_NothingPlayingName"));
                return (MediaView, "panel-media-empty", EmptyMediaSize);
            }

            MediaView.Update(media, now);
            AutomationProperties.SetName(Surface, string.Join(", ", new[] { media.Title, media.Artist }.Where(t => !string.IsNullOrEmpty(t))));
            return (MediaView, "panel-media", MediaPanelSize);
        }

        if (page == PanelPage.Shelf)
        {
            ShelfView.SetInset(inset);
            ShelfView.Animate = Springs;
            ShelfView.Update(_scene.Shelf);
            ShelfView.SetDropping(_dragging);
            AutomationProperties.SetName(Surface, Strings.Format("Island_Shelf_CountFormat", _scene.Shelf.Count));
            return (ShelfView, "panel-shelf", ShelfPanelSize);
        }

        TimerView.SetInset(inset);
        TimerView.Update(_scene, now, Springs);
        bool timing = _scene.Timer?.IsActive == true;
        AutomationProperties.SetName(Surface, timing ? IslandPresenter.FormatRemaining(_scene.Timer!.RemainingAt(now)) : IslandPresenter.FormatClock(now));
        return (TimerView, $"panel-timer-{timing}", PanelSize);
    }

    /// <summary>
    /// Every page that is turned on, so each can always be reached from the rail. Music and the shelf show an empty
    /// state when there is nothing in them.
    /// </summary>
    private List<PanelPage> AvailablePages()
    {
        List<PanelPage> pages = [];
        if (_scene.Settings.ShowMedia || _scene.Media is not null)
        {
            pages.Add(PanelPage.Media);
        }

        pages.Add(PanelPage.Timer);
        if (_scene.Settings.ShelfEnabled)
        {
            pages.Add(PanelPage.Shelf);
        }

        return pages;
    }

    /// <summary>Opens on what the pill was showing: a running timer, the shelf, the music, else the clock and timers.</summary>
    private PanelPage DefaultPage(List<PanelPage> pages) => _scene.Primary?.Kind switch
    {
        _ when _dragging && pages.Contains(PanelPage.Shelf) => PanelPage.Shelf,
        ActivityKind.Timer or ActivityKind.TimerDone => PanelPage.Timer,
        ActivityKind.Shelf when pages.Contains(PanelPage.Shelf) => PanelPage.Shelf,
        _ => _scene.Media is not null && pages.Contains(PanelPage.Media) ? PanelPage.Media : PanelPage.Timer,
    };

    private void ShowPage(PanelPage page)
    {
        if (page != _page)
        {
            _page = page;
            Update();
        }
    }

    private void ShowRail(List<PanelPage> pages, PanelPage selected)
    {
        PanelRail.Visibility = pages.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        (Button Tab, PanelPage Page)[] tabs = [(MediaTab, PanelPage.Media), (TimerTab, PanelPage.Timer), (ShelfTab, PanelPage.Shelf)];
        foreach ((Button tab, PanelPage tabPage) in tabs)
        {
            bool isSelected = tabPage == selected;
            tab.Visibility = pages.Contains(tabPage) ? Visibility.Visible : Visibility.Collapsed;
            tab.Background = isSelected ? new SolidColorBrush(Windows.UI.Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)) : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            tab.Foreground = new SolidColorBrush(isSelected ? IslandPalette.TextPrimary : IslandPalette.TextSecondary);
        }
    }

    /// <summary>Plays the entrance when the content changes; returns true when it did.</summary>
    private bool ShowView(UIElement view, string key)
    {
        if (key == _shownKey)
        {
            return false;
        }

        UIElement? previous = _shownView;
        _shownView = view;
        _shownKey = key;
        if (previous is not null && previous != view)
        {
            IslandMotion.Exit(previous, () => _shownView != previous);
        }

        UIElement[]? rows = view == MediaView ? MediaView.Rows : view == TimerView ? TimerView.Rows : view == ShelfView ? ShelfView.Rows : null;
        if (rows is not null)
        {
            // Panel rows arrive one after the other.
            IslandMotion.Enter(view, TimeSpan.Zero, Springs);
            for (int i = 0; i < rows.Length; i++)
            {
                IslandMotion.Enter(rows[i], IslandMotion.Stagger * (i + 1), Springs);
            }
        }
        else
        {
            IslandMotion.Enter(view, previous is null ? TimeSpan.Zero : IslandMotion.Stagger, Springs);
        }

        return true;
    }

    private void MorphTo(Size size)
    {
        Surface.Width = size.Width;
        Surface.Height = size.Height;
        _shape.MorphTo(new Vector2((float)size.Width, (float)size.Height), Springs);
        var bounds = new Rect((StageWidth - size.Width) / 2, TopInset, size.Width, size.Height);
        Rect region = bounds;
        if (_bubbleShown)
        {
            region.Union(new Rect(bounds.Right + BubbleGap, TopInset + ((CompactHeight - BubbleSize) / 2), BubbleSize, BubbleSize));
        }

        TargetBounds = bounds;
        if (region != RegionBounds)
        {
            RegionBounds = region;
            TargetChanged?.Invoke(this, region);
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
        bool timerRunning = _scene.Timer?.State == TimerState.Running;
        bool counting = _mode == PillMode.Panel
            ? (_page == PanelPage.Timer && timerRunning) || (_page == PanelPage.Media && _scene.Media?.IsPlaying == true)
            : _scene.Primary?.Kind == ActivityKind.Timer && timerRunning;
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
        else
        {
            // The next time the panel opens, it opens on whatever is most relevant then.
            _page = null;
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
        if (e.GetCurrentPoint(Surface).Properties.IsMiddleButtonPressed)
        {
            // Middle-click waves an alert away, like closing a tab.
            if (_scene.Primary is { IsAlert: true })
            {
                Commands?.DismissAlert();
            }

            e.Handled = true;
            return;
        }

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

    /// <summary>A file dragged onto the island opens the shelf with its drop zone glowing.</summary>
    private void OnDragEnter(object sender, DragEventArgs e)
    {
        _dragLeaveTimer.Stop();
        if (_dragging || ShelfView.IsDraggingOut || !_scene.Settings.ShelfEnabled || !e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        _dragging = true;
        _peekTimer.Stop();
        _closeTimer.Stop();
        _lingerTimer.Stop();
        _page = PanelPage.Shelf;
        if (_mode == PillMode.Panel)
        {
            Update();
        }
        else
        {
            SetMode(PillMode.Panel);
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        // The shelf keeps a reference to the file and never moves it, so it asks for a link (or a copy, which the
        // source treats the same way: the original stays where it is).
        e.AcceptedOperation = e.AllowedOperations.HasFlag(DataPackageOperation.Link) ? DataPackageOperation.Link : e.AllowedOperations & DataPackageOperation.Copy;
        e.DragUIOverride.Caption = Strings.Get("Island_ShelfDropCaption");
        e.Handled = true;
    }

    /// <summary>Moving between the island's own parts raises leave then enter, so the drag ends only after a short pause.</summary>
    private void OnDragLeave(object sender, DragEventArgs e)
    {
        if (_dragging)
        {
            _dragLeaveTimer.Start();
        }
    }

    private void EndDrag()
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        Update();
        if (!_hovering)
        {
            _closeTimer.Start();
        }
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragLeaveTimer.Stop();
        _dragging = false;
        DragOperationDeferral deferral = e.GetDeferral();
        try
        {
            IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();
            Commands?.AddToShelf(items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)));
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or UnauthorizedAccessException)
        {
            // The source took the data back (or never had real files); nothing lands on the shelf.
            e.AcceptedOperation = DataPackageOperation.None;
        }
        finally
        {
            deferral.Complete();
        }

        // The tool's update brings the new tiles; meanwhile the zone settles. The shelf stays open a moment to show them.
        Update();
        _shape.Bounce(Springs);
        _lingerTimer.Start();
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
