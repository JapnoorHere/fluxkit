using System.Numerics;
using FluxKit.App.Common;
using FluxKit.App.Modules.FluxIsland.Design;
using FluxKit.Platform.Shell;
using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
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
    public const double StageWidth = 460;
    public const double StageHeight = 320;
    public const double TopInset = 6;

    private const double CompactHeight = 36;
    private const double MinCompactWidth = 150;
    private const double MaxCompactWidth = 300;
    private const double DotsRoom = 20;
    private const double PageDotsRoom = 18;
    private const float PageSlide = 28;
    private const float HoverScale = 1.04f;
    private const float PressScale = 0.94f;
    private const float MaxLean = 3;
    private const int WheelNotch = 120;
    private const double BubbleSize = 36;
    private const double BubbleGap = 8;
    private static readonly Size IdleSize = new(120, 32);
    private static readonly Size BareSize = new(88, 28);
    private static readonly Size PeekSize = new(320, 52);
    private static readonly TimeSpan PeekDelay = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan CloseDelay = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan DragLeaveDelay = TimeSpan.FromMilliseconds(80);
    private static readonly TimeSpan DropLingerDelay = TimeSpan.FromMilliseconds(1800);

    private readonly PillShape _shape;
    private readonly AlbumArt _art = new();
    private readonly LevelMeter _meter;
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
    private bool _held;
    private Vector3 _lean;
    private bool _glass;
    private bool _highContrast;
    private bool _struck;
    private int _pageSlide;
    private bool _keyboard;

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
        _meter = new LevelMeter(DispatcherQueue);
        _meter.Changed += (_, level) => ShowLevel(level);
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
        _closeTimer = OneShot(CloseDelay, () =>
        {
            if (!_held && !_keyboard)
            {
                SetMode(PillMode.Compact);
            }
        });
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

        ShelfView.ShareRequested += (_, files) => ShareRequested?.Invoke(this, files);
        ShelfView.MenuShown += (_, open) =>
        {
            // Keep the panel open under a tile's menu, then close as usual if the pointer has left.
            _held = open;
            if (!open && !_hovering && _mode != PillMode.Compact)
            {
                _closeTimer.Start();
            }
        };
        MediaTab.Click += (_, _) => ShowPage(PanelPage.Media);
        TimerTab.Click += (_, _) => ShowPage(PanelPage.Timer);
        ShelfTab.Click += (_, _) => ShowPage(PanelPage.Shelf);
        GlanceTab.Click += (_, _) => ShowPage(PanelPage.Glance);
        NotesTab.Click += (_, _) => ShowPage(PanelPage.Notes);
        RewindTab.Click += (_, _) => ShowPage(PanelPage.Rewind);
        ClipsTab.Click += (_, _) => ShowPage(PanelPage.Clips);
        ConfigureLists();
        WireCommandBar();
        GlanceView.CloseRequested += (_, _) => SetMode(PillMode.Compact);
        TimerView.SwitchChanged += (_, _) => Update();
        Surface.KeyDown += OnKeyDown;
        PressFeedback.Attach(MediaTab);
        PressFeedback.Attach(TimerTab);
        PressFeedback.Attach(ShelfTab);
        PressFeedback.Attach(GlanceTab);

        Surface.PointerEntered += OnPointerEntered;
        Surface.PointerExited += OnPointerExited;
        Surface.PointerMoved += OnPointerMoved;
        ElementCompositionPreview.SetIsTranslationEnabled(Stage, true);
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

        /// <summary>The command bar, opened by the shortcut.</summary>
        Command,
    }

    /// <summary>True while the island is open: the panel or the command bar.</summary>
    private bool IsOpen => _mode is PillMode.Panel or PillMode.Command;

    private enum PanelPage
    {
        Media,
        Timer,
        Shelf,
        Glance,
        Notes,
        Rewind,
        Clips,
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
            GlanceView.Commands = value;
        }
    }

    /// <summary>False when Windows animations are off or the tool uses Light effects; springs become short fades.</summary>
    public bool Springs { get; set; } = true;

    /// <summary>The pill's resting bounds in stage DIPs. The window keeps its click region in step with it.</summary>
    public Rect TargetBounds { get; private set; }

    /// <summary>The pill plus the bubble, when it shows. The window's click region follows this.</summary>
    public Rect RegionBounds { get; private set; }

    public event EventHandler<Rect>? TargetChanged;

    /// <summary>Files the user asked to share; the window opens the share sheet, which needs its handle.</summary>
    public event EventHandler<IReadOnlyList<string>>? ShareRequested;

    /// <summary>The panel that the keyboard opened has closed; the window gives focus back to where it was.</summary>
    public event EventHandler? KeyboardReleased;

    /// <summary>True while the panel is open from the keyboard shortcut and holds keyboard focus.</summary>
    public bool IsKeyboardOpen => _keyboard;

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
    /// <param name="sample">The sample's name; "glance", "notes" and "command" open on that page or on the command bar.</param>
    public void Preview(IslandScene scene, bool open, bool dropping = false, string? sample = null)
    {
        _mode = sample == "command" ? PillMode.Command
            : open || dropping || sample is "glance" or "notes" or "rewind" or "clips" ? PillMode.Panel
            : PillMode.Compact;
        _page = sample switch
        {
            "glance" => PanelPage.Glance,
            "notes" => PanelPage.Notes,
            "rewind" => PanelPage.Rewind,
            "clips" => PanelPage.Clips,
            _ => null,
        };
        if (sample == "command")
        {
            CommandView.ShowText("= 12*8+3");
        }

        _dragging = dropping;
        _previewDrop = dropping;
        Show(scene);
    }

    private void Show(IslandScene scene)
    {
        _scene = scene;
        Surface.AllowDrop = scene.Settings.ShelfEnabled;
        ApplyLook(scene.Settings.Look == FluxKit.Tools.FluxIsland.IslandLook.Glass);
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
            GlanceView.SetLive(false);
            _meter.SetRunning(false);
            _callTimer?.Stop();
            ReleaseKeyboard();
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
        (UIElement view, string key, Size size) = _mode switch
        {
            PillMode.Panel => BuildPanel(now),
            PillMode.Command => BuildCommand(),
            _ => BuildCompact(now),
        };
        bool changed = ShowView(view, key);
        UpdateGlow(now, flare: changed && view == CompactView && _scene.Primary is { IsAlert: true });
        if (changed && view == CompactView && _scene.Primary is { IsAlert: true } alert)
        {
            PlayArrival(alert);
        }

        UpdateBubble(view == CompactView && _mode == PillMode.Compact ? _scene.Secondary : null, size, now);
        MorphTo(size);
        ScheduleTimers(now);
        GlanceView.SetLive(_active && _mode == PillMode.Panel && _page == PanelPage.Glance);
        UpdateMeter(view);
        UpdateCallListener();
        OfferHint();
    }

    /// <summary>
    /// True while Windows high contrast is on: the glow is dropped and the pill gets a solid white outline, so its
    /// edge is clear against any background.
    /// </summary>
    public bool HighContrast
    {
        get => _highContrast;
        set
        {
            if (value != _highContrast)
            {
                _highContrast = value;
                _shape.SetHighContrast(value);
            }
        }
    }

    /// <summary>When the user turned sounds on, a soft Windows sound for the moments that need attention.</summary>
    private void PlaySound(IslandActivity alert)
    {
        if (!_scene.Settings.Sounds || Commands is null)
        {
            return;
        }

        if (alert.Kind == ActivityKind.TimerDone)
        {
            FluxKit.Platform.Shell.SystemSounds.Play(FluxKit.Platform.Shell.SoundCue.Reminder);
        }
        else if (alert.Kind is ActivityKind.LowBattery or ActivityKind.Offline || (alert.Kind == ActivityKind.MicMuted && alert.Value == 1))
        {
            FluxKit.Platform.Shell.SystemSounds.Play(FluxKit.Platform.Shell.SoundCue.Notification);
        }
    }

    /// <summary>Tells Narrator and other screen readers about a moment as it arrives, without moving focus.</summary>
    private void Announce(IslandActivity alert)
    {
        CompactContent content = IslandPresenter.Describe(alert, DateTimeOffset.Now);
        string text = string.Join(", ", new[] { content.Title, content.Subtitle, content.Trail }.Where(t => !string.IsNullOrEmpty(t)));
        Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.FromElement(Surface)?.RaiseNotificationEvent(
            Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationKind.Other,
            Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationProcessing.ImportantMostRecent,
            text,
            "FluxIslandMoment");
    }

    /// <summary>
    /// The one gentle motion a live moment carries (plan I1, I6): a call's badge breathes while you can be heard, and a
    /// slash draws across the mic when it is muted. Started only when the state changes, so updates cost nothing.
    /// </summary>
    private void ShowIconLife(IslandActivity? activity)
    {
        bool live = activity is { Kind: ActivityKind.Call, Flag: false };
        CompactGlyph.Breathe(live && _active, Springs);
        bool muted = activity is { Kind: ActivityKind.MicMuted, Flag: true } or { Kind: ActivityKind.Call, Flag: true };
        if (muted != _struck)
        {
            _struck = muted;
            CompactGlyph.Strike(muted, Springs);
        }
    }

    /// <summary>Each kind of alert arrives with its own small signature, so moments feel distinct, not generic.</summary>
    private void PlayArrival(IslandActivity alert)
    {
        Announce(alert);
        PlaySound(alert);
        switch (alert.Kind)
        {
            case ActivityKind.PluggedIn:
                CompactRing.Sweep(alert.Value, Springs);
                CompactRing.Flash(Springs);
                break;
            case ActivityKind.TimerDone:
                // Time's up deserves the most: the bell rings, a ring bursts out of it and the pill bounces once.
                IslandMotion.Shake(CompactGlyph, Springs);
                IslandMotion.Burst(CompactBurst, Springs);
                _shape.Bounce(Springs);
                break;
            case ActivityKind.Copied:
                IslandMotion.Flip(CompactGlyph, Springs);
                break;
            case ActivityKind.Screenshot:
                IslandMotion.Drop(CompactArtFrame, Springs);
                IslandMotion.Pop(CompactGlyph, Springs);
                break;
            case ActivityKind.DeviceConnected or ActivityKind.DeviceDisconnected or ActivityKind.AudioDevice or ActivityKind.DesktopSwitched:
                IslandMotion.SlideIn(CompactGlyph, Springs);
                break;
            case ActivityKind.LowBattery or ActivityKind.MicMuted or ActivityKind.Offline:
                IslandMotion.Shake(CompactGlyph, Springs);
                break;
            default:
                IslandMotion.Pop(CompactGlyph, Springs);
                break;
        }
    }

    /// <summary>
    /// Tints the glow around the pill with the identity colour of what it shows: the album's colour for music,
    /// a moment's own colour otherwise. Alerts flare as they arrive; quiet status moments and the idle clock don't glow.
    /// </summary>
    private void UpdateGlow(DateTimeOffset now, bool flare)
    {
        if (HighContrast)
        {
            // High contrast: no colour glow, just the solid outline.
            _shape.SetAccent(IslandPalette.TextPrimary, 0, Springs);
            return;
        }

        if (_mode == PillMode.Command)
        {
            _shape.SetAccent(IslandPalette.Blue, 0.4f, Springs);
            return;
        }

        if (_mode == PillMode.Panel)
        {
            // The open panel glows softly in the colour of its page: the album for music, orange for timers.
            (Windows.UI.Color pageAccent, float pageGlow) = _page switch
            {
                PanelPage.Media when _scene.Media is not null => (_art.Accent, 0.5f),
                PanelPage.Timer when _scene.Timer?.IsActive == true || _scene.Stopwatch?.IsActive == true => (IslandPalette.Orange, 0.35f),
                _ => (IslandPalette.TextPrimary, 0f),
            };
            _shape.SetAccent(pageAccent, pageGlow, Springs);
            return;
        }

        if (_scene.Primary is not { } shown)
        {
            _shape.SetAccent(IslandPalette.TextPrimary, 0, Springs);
            return;
        }

        Windows.UI.Color accent = shown.Kind == ActivityKind.Media ? _art.Accent : IslandPresenter.Describe(shown, now).Accent;
        float intensity = accent == IslandPalette.TextPrimary ? 0 : IslandPalette.GlowFor(shown);
        _shape.SetAccent(accent, intensity, Springs);
        if (flare)
        {
            _shape.Flare(Springs);
        }
    }

    /// <summary>Runs the peek chip's action for <paramref name="activity"/> (see <see cref="IslandPresenter.ActionLabel"/>).</summary>
    private void RunAction(IslandActivity? activity)
    {
        switch (activity)
        {
            case { Kind: ActivityKind.MicMuted or ActivityKind.Call }:
                Commands?.ToggleMicMute();
                break;
            case { Kind: ActivityKind.DownloadDone, Path: { } download }:
                // Shown, never opened: a download could be a program, and running one should be the user's own click.
                ShellLauncher.ShowInFolder(download);
                Commands?.DismissAlert();
                break;
            case { Kind: ActivityKind.FileReady, Path: { } made, Value: not (int)FileResult.PathCopied }:
                ShellLauncher.ShowInFolder(made);
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
        // Music shows its art instead of the note glyph, and the bouncing bars on the trailing edge.
        MediaInfo? media = activity?.Kind == ActivityKind.Media ? activity.Media : null;
        // Moments with a level show their icon inside a progress ring.
        bool ring = content.Progress is not null;
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
        ring &= !art;
        CompactRing.Visibility = ring ? Visibility.Visible : Visibility.Collapsed;
        if (ring)
        {
            CompactRing.SetGlyph(content.Glyph);
            CompactRing.SetColor(content.Accent);
            CompactRing.SetProgress(content.Progress!.Value, Springs);
        }

        CompactDots.Update(_scene.Privacy, Springs);
        CompactArtFrame.Visibility = art ? Visibility.Visible : Visibility.Collapsed;
        CompactBars.Visibility = media is null ? Visibility.Collapsed : Visibility.Visible;
        if (media is not null)
        {
            CompactBars.SetColor(_art.Accent);
            CompactBars.SetPlaying(media.IsPlaying, Springs);
            content = content with { Accent = _art.Accent };
        }

        // Leading: every moment shows its icon in a badge of its colour (plan §4.1).
        CompactGlyph.Show(content.Glyph, content.Accent);
        CompactGlyph.Visibility = content.Glyph.Length > 0 && !art && !ring ? Visibility.Visible : Visibility.Collapsed;
        ShowIconLife(activity);
        bool level = activity?.Kind is ActivityKind.Volume or ActivityKind.Brightness;
        CompactLevel.Visibility = level ? Visibility.Visible : Visibility.Collapsed;
        CompactTitle.Visibility = level || content.Title.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (level)
        {
            SetLevel(activity!.Value);
        }

        // Middle: a label. It is white when it is the message itself, and steps back when a value follows it.
        CompactTitle.Text = content.Title;
        CompactTitle.Foreground = new SolidColorBrush(content.Trail is null || twoLines ? IslandPalette.TextPrimary : IslandPalette.TextSecondary);
        CompactSubtitle.Text = content.Subtitle ?? string.Empty;
        CompactSubtitle.Visibility = twoLines ? Visibility.Visible : Visibility.Collapsed;
        if (twoLines && content.Title.Length == 0)
        {
            // Value-only moments (a timer, a call) say what they are when peeked at.
            CompactTitle.Text = content.Subtitle ?? string.Empty;
            CompactTitle.Visibility = Visibility.Visible;
            CompactSubtitle.Visibility = Visibility.Collapsed;
        }

        // Trailing: the value, large and in the moment's colour.
        CompactTrail.Text = content.Trail ?? string.Empty;
        CompactTrail.Foreground = new SolidColorBrush(content.Accent == IslandPalette.TextPrimary ? IslandPalette.TextPrimary : content.Accent);
        CompactTrail.Visibility = content.Trail is null ? Visibility.Collapsed : Visibility.Visible;
        string? action = _mode == PillMode.Peek && Commands is not null && activity is not null ? IslandPresenter.ActionLabel(activity) : null;
        CompactAction.Content = action;
        CompactAction.Visibility = action is null ? Visibility.Collapsed : Visibility.Visible;
        CompactAction.Background = new SolidColorBrush(IslandPalette.Tint(content.Accent));
        CompactAction.Foreground = new SolidColorBrush(content.Accent);
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

    private void MorphTo(Size size)
    {
        _size = size;
        Surface.Width = size.Width;
        Surface.Height = size.Height;
        _shape.MorphTo(new Vector2((float)size.Width, (float)size.Height), Springs);
        var bounds = new Rect((StageWidth - size.Width) / 2, TopInset, size.Width, size.Height);
        Rect region = bounds;
        if (_bubbleShown)
        {
            region.Union(new Rect(bounds.Right + BubbleGap, TopInset + ((CompactHeight - BubbleSize) / 2), BubbleSize, BubbleSize));
        }

        if (Hint.Visibility == Visibility.Visible)
        {
            PlaceHint();
            region.Union(HintBounds());
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
        bool stopwatchRunning = _scene.Stopwatch?.State == TimerState.Running;
        bool counting = _mode == PillMode.Panel
            ? (_page == PanelPage.Timer && (timerRunning || stopwatchRunning)) || (_page == PanelPage.Media && _scene.Media?.IsPlaying == true)
            : (_scene.Primary?.Kind == ActivityKind.Timer && timerRunning) || (_scene.Primary?.Kind == ActivityKind.Stopwatch && stopwatchRunning) || _scene.Primary?.Kind == ActivityKind.Call;
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
        if (IsOpen)
        {
            _shape.ScaleTo(1, Springs);
            LeanTo(Vector3.Zero);
        }
        else
        {
            // The next time the panel opens, it opens on whatever is most relevant then.
            _page = null;
            ReleaseKeyboard();
        }

        Update();
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
