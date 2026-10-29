using FluxKit.Platform.VirtualDesktop;
using FluxKit.Platform.VirtualDesktop.Interop;
using FluxKit.Platform.Windows;
using FluxKit.Utilities.DragToSwitch;
using FluxKit.Utilities.DragToSwitch.Interaction;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.Win32;
using Serilog;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;

namespace FluxKit.App.Modules.DragToSwitch;

/// <summary>
/// The desktop selector overlay (Docs/PLAN.md §8.2). A pre-created, non-activating, topmost tool window
/// that never takes focus or mouse capture, so the OLE drag in the source app is untouched.
/// </summary>
public sealed partial class SelectorWindow : Window
{
    private static readonly TimeSpan Entrance = TimeSpan.FromMilliseconds(167);
    private static readonly TimeSpan Exit = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan Hover = TimeSpan.FromMilliseconds(83);

    private readonly IVirtualDesktopService _desktops;
    private readonly ILogger _log;
    private readonly nint _hwnd;
    private readonly List<CardVisual> _cards = [];
    private readonly Dictionary<string, BitmapImage> _imageCache = [];
    private readonly DispatcherQueueTimerHolder _timers;
    private DragToSwitchSettings _settings = new();
    private SelectorGeometry? _geometry;
    private bool _pinned;
    private bool _visible;
    private DragKind _kind;
    private int _showVersion;

    public SelectorWindow(IVirtualDesktopService desktops, ILogger log)
    {
        _desktops = desktops;
        _log = log.ForContext<SelectorWindow>();
        InitializeComponent();
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _timers = new DispatcherQueueTimerHolder(DispatcherQueue);
        View = new ViewAdapter(this);

        AppWindow.IsShownInSwitchers = false;
        AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "FluxKit.ico"));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        SystemBackdrop = new DesktopAcrylicBackdrop();
        WindowHelpers.MakeOverlay(_hwnd);

        Root.DragEnter += OnDragOverSelector;
        Root.DragOver += OnDragOverSelector;
        Root.Drop += (_, e) => e.AcceptedOperation = DataPackageOperation.None;
        Closed += (_, _) => _timers.StopAll();
    }

    /// <summary>Thread-safe view used by the coordinator.</summary>
    public ISelectorView View { get; }

    public void ApplyTheme(ElementTheme theme) => Root.RequestedTheme = theme;

    /// <summary>Re-pins after Explorer restarts.</summary>
    public void RePin()
    {
        _pinned = false;
        if (_visible)
        {
            _ = PinAsync();
        }
    }

    /// <summary>Shows the selector for four seconds with sample state; never switches.</summary>
    public void ShowPreview(DragToSwitchSettings settings)
    {
        IReadOnlyList<VirtualDesktopInfo> desktops = _desktops.Desktops;
        List<CardModel> cards = desktops.Count > 0
            ? desktops.Select(d => new CardModel(d.Id, d.Index + 1, d.Name, d.WallpaperPath, d.IsCurrent)).ToList()
            : [new CardModel(Guid.NewGuid(), 1, "Desktop 1", null, true), new CardModel(Guid.NewGuid(), 2, "Desktop 2", null, false)];
        (int x, int y) = WindowHelpers.GetCursorPosition();
        (PixelRect work, double scale) = WindowHelpers.GetMonitorAt(x, y);
        SelectorGeometry geometry = SelectorLayout.Compute(cards.Count, settings.OverlaySize, settings.PreviewStyle, settings.OverlayPosition, work, scale, x, y - 200);
        ShowCore(cards, geometry, settings, DragKind.File);
        _log.Information("Selector preview shown at {Overlay} ({Count} cards, visible={Visible})", geometry.Overlay, cards.Count, AppWindow.IsVisible);
        int sample = cards.FindIndex(c => !c.IsCurrent);
        SetCardStateCore(sample, CardVisualState.Dwelling, TimeSpan.FromMilliseconds(settings.HoverDelayMs), null);
        Instruction.Text = "Preview: this is how the picker looks while you drag";
        int version = _showVersion;
        _timers.Once(TimeSpan.FromSeconds(4), () =>
        {
            if (version == _showVersion)
            {
                HideCore();
            }
        });
    }

    private string HoldText => _kind == DragKind.Window ? "Hold over a desktop to move this window there" : "Hold over a desktop to go there";

    private void ShowCore(IReadOnlyList<CardModel> cards, SelectorGeometry geometry, DragToSwitchSettings settings, DragKind kind)
    {
        _kind = kind;
        _showVersion++;
        _timers.StopAll();
        _settings = settings;
        _geometry = geometry;
        Expanded.Visibility = Visibility.Visible;
        Pill.Visibility = Visibility.Collapsed;
        Root.Opacity = 1;
        Instruction.Text = HoldText;
        Hint.Visibility = settings.NumberKeysEnabled ? Visibility.Visible : Visibility.Collapsed;
        BuildCards(cards, geometry);
        MoveToCurrentDesktopIfNeeded();
        Place(geometry.Overlay);
        _visible = true;
        if (!_pinned)
        {
            _ = PinAsync();
        }

        if (AnimationsOn)
        {
            double from = geometry.AnchoredBottom ? 12 : -12;
            Animate(Root, "Opacity", 0, 1, Entrance, decelerate: true);
            Animate(RootTranslate, "Y", from, 0, Entrance, decelerate: true);
            for (int i = 0; i < _cards.Count && i < 8; i++)
            {
                _cards[i].Root.Opacity = 0;
                Animate(_cards[i].Root, "Opacity", 0, 1, Entrance, decelerate: true, beginAfter: TimeSpan.FromMilliseconds(20 * i));
            }
        }
        else
        {
            RootTranslate.Y = 0;
        }
    }

    private void UpdateCardsCore(IReadOnlyList<CardModel> cards, SelectorGeometry geometry)
    {
        bool sameIds = cards.Count == _cards.Count && cards.Select(c => c.DesktopId).SequenceEqual(_cards.Select(c => c.Model.DesktopId));
        _geometry = geometry;
        if (sameIds)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                _cards[i].Bind(cards[i]);
            }

            return;
        }

        BuildCards(cards, geometry);
        if (_visible && Expanded.Visibility == Visibility.Visible)
        {
            Place(geometry.Overlay);
        }
    }

    private void SetCardStateCore(int? index, CardVisualState state, TimeSpan delay, string? message)
    {
        if (index is not int i || i < 0 || i >= _cards.Count)
        {
            return;
        }

        CardVisual card = _cards[i];
        card.SetState(state, delay, AnimationsOn);
        switch (state)
        {
            case CardVisualState.Switching:
                Instruction.Text = $"Switching to {card.Model.Name}…";
                break;
            case CardVisualState.Switched:
                Instruction.Text = _kind == DragKind.Window
                    ? $"Now on {card.Model.Name}. Let go to place the window."
                    : $"Now on {card.Model.Name}. Drop the file into any app.";
                Announce($"Switched to {card.Model.Name}");
                break;
            case CardVisualState.Error:
                Instruction.Text = message ?? "Couldn't switch. Try Ctrl+Win+Left or Right arrow.";
                Announce(Instruction.Text);
                _timers.Once(TimeSpan.FromSeconds(2), () =>
                {
                    if (card.State == CardVisualState.Error)
                    {
                        card.SetState(CardVisualState.Rest, TimeSpan.Zero, AnimationsOn);
                        Instruction.Text = HoldText;
                    }
                });
                break;
            case CardVisualState.Rest:
            case CardVisualState.Dwelling:
                break;
        }
    }

    private void SetCursorOverSelectorCore(bool over)
    {
        if (Expanded.Visibility == Visibility.Visible && !_cards.Any(c => c.State is CardVisualState.Switching or CardVisualState.Error))
        {
            // Releasing a window over the selector just places it there, so only files need the warning.
            Instruction.Text = over && _kind == DragKind.File ? "To drop, let go outside the picker" : HoldText;
        }
    }

    private void CollapseCore(CardModel destination)
    {
        if (_geometry is null)
        {
            return;
        }

        PillText.Text = _kind == DragKind.Window
            ? $"On {destination.Name}. Let go to place the window"
            : $"On {destination.Name}. Drop into any app";
        AutomationProperties.SetName(Pill, PillText.Text);
        Expanded.Visibility = Visibility.Collapsed;
        Pill.Visibility = Visibility.Visible;
        PillChevron.Glyph = _geometry.AnchoredBottom ? "" : "";
        RootTranslate.Y = 0;
        Root.Opacity = 1;

        // Not pinned → the window stayed on the previous desktop; bring it to the one we just switched to.
        MoveToCurrentDesktopIfNeeded();
        Place(_geometry.Pill);
        WindowHelpers.BringToTopNoActivate(_hwnd);
        int version = ++_showVersion;
        _timers.Once(TimeSpan.FromMilliseconds(1500), () =>
        {
            if (version == _showVersion && Pill.Visibility == Visibility.Visible)
            {
                if (AnimationsOn)
                {
                    Animate(Root, "Opacity", 1, 0.7, TimeSpan.FromMilliseconds(250), decelerate: true);
                }
                else
                {
                    Root.Opacity = 0.7;
                }
            }
        });
    }

    private void ExpandCore()
    {
        if (_geometry is null)
        {
            return;
        }

        _showVersion++;
        Pill.Visibility = Visibility.Collapsed;
        Expanded.Visibility = Visibility.Visible;
        Root.Opacity = 1;
        Instruction.Text = HoldText;
        foreach (CardVisual card in _cards)
        {
            card.SetState(CardVisualState.Rest, TimeSpan.Zero, animate: false);
        }

        Place(_geometry.Overlay);
        if (AnimationsOn)
        {
            Animate(Root, "Opacity", 0.6, 1, Entrance, decelerate: true);
        }
    }

    private void HideCore()
    {
        if (!_visible)
        {
            return;
        }

        _visible = false;
        int version = ++_showVersion;
        foreach (CardVisual card in _cards)
        {
            card.StopAnimations();
        }

        if (AnimationsOn && Expanded.Visibility == Visibility.Visible)
        {
            Animate(Root, "Opacity", Root.Opacity, 0, Exit, decelerate: false);
            Animate(RootTranslate, "Y", 0, 8, Exit, decelerate: false);
            _timers.Once(Exit, () =>
            {
                if (version == _showVersion)
                {
                    AppWindow.Hide();
                }
            });
        }
        else
        {
            AppWindow.Hide();
        }
    }

    private bool AnimationsOn => _settings.AnimationsEnabled && new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;

    private void Place(PixelRect rect)
    {
        AppWindow.MoveAndResize(new RectInt32(rect.Left, rect.Top, rect.Width, rect.Height));

        // A borderless WinUI window can still keep a few pixels of non-client frame, which would clip the
        // bottom of the cards. Grow the window by the difference (upwards when anchored to the bottom).
        int extraW = rect.Width - AppWindow.ClientSize.Width;
        int extraH = rect.Height - AppWindow.ClientSize.Height;
        if (extraW > 0 || extraH > 0)
        {
            extraW = Math.Max(0, extraW);
            extraH = Math.Max(0, extraH);
            int top = _geometry?.AnchoredBottom == true ? rect.Top - extraH : rect.Top;
            AppWindow.MoveAndResize(new RectInt32(rect.Left - (extraW / 2), top, rect.Width + extraW, rect.Height + extraH));
        }

        if (!AppWindow.IsVisible)
        {
            AppWindow.Show(activateWindow: false);
        }

        WindowHelpers.BringToTopNoActivate(_hwnd);
    }

    private async Task PinAsync()
    {
        _pinned = await _desktops.PinWindowAsync(_hwnd);
        _log.Debug("Selector pinned to all desktops: {Pinned}", _pinned);
    }

    private void MoveToCurrentDesktopIfNeeded()
    {
        if (_pinned)
        {
            return;
        }

        Guid current = _desktops.CurrentDesktopId;
        if (current != Guid.Empty && PublicVirtualDesktopManager.GetWindowDesktopId(_hwnd) is Guid mine && mine != current)
        {
            PublicVirtualDesktopManager.MoveOwnWindowToDesktop(_hwnd, current);
        }
    }

    private void BuildCards(IReadOnlyList<CardModel> cards, SelectorGeometry geometry)
    {
        foreach (CardVisual old in _cards)
        {
            old.StopAnimations();
        }

        _cards.Clear();
        CardsPanel.Children.Clear();
        double scale = geometry.Scale;
        string? fallbackWallpaper = _settings.PreviewStyle == PreviewStyle.Wallpaper ? CurrentWallpaper() : null;
        for (int i = 0; i < cards.Count; i++)
        {
            PixelRect rect = geometry.Cards[i];
            var visual = new CardVisual(rect.Width / scale, rect.Height / scale, _settings.PreviewStyle != PreviewStyle.None, LoadImage);
            visual.Bind(cards[i], fallbackWallpaper);
            _cards.Add(visual);
            CardsPanel.Children.Add(visual.Root);
        }
    }

    private BitmapImage? LoadImage(string? path, double widthDip)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }

        string key = $"{path}|{File.GetLastWriteTimeUtc(path).Ticks}";
        if (!_imageCache.TryGetValue(key, out BitmapImage? image))
        {
            image = new BitmapImage { DecodePixelWidth = (int)(widthDip * (_geometry?.Scale ?? 1)), UriSource = new Uri(path) };
            _imageCache[key] = image;
        }

        return image;
    }

    private static string? CurrentWallpaper()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
        return key?.GetValue("WallPaper") as string;
    }

    private void Announce(string text)
    {
        AutomationPeer? peer = FrameworkElementAutomationPeer.FromElement(Root) ?? FrameworkElementAutomationPeer.CreatePeerForElement(Root);
        peer?.RaiseNotificationEvent(AutomationNotificationKind.ActionCompleted, AutomationNotificationProcessing.ImportantMostRecent, text, "FluxKitSelector");
    }

    private void OnDragOverSelector(object sender, DragEventArgs e)
    {
        // FluxKit never accepts the drop: a "move" reported here could make the source delete its file.
        e.AcceptedOperation = DataPackageOperation.None;
        e.DragUIOverride.Caption = "Drop outside the picker";
        e.DragUIOverride.IsGlyphVisible = true;
    }

    private static void Animate(DependencyObject target, string property, double from, double to, TimeSpan duration, bool decelerate, TimeSpan? beginAfter = null)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = duration,
            BeginTime = beginAfter,
            EasingFunction = new ExponentialEase { EasingMode = decelerate ? EasingMode.EaseOut : EasingMode.EaseIn, Exponent = 4.5 },
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    /// <summary>Visual tree and state for one desktop card.</summary>
    private sealed class CardVisual
    {
        private readonly Border _border;
        private readonly Border _preview;
        private readonly Image _image;
        private readonly TextBlock _bigNumber;
        private readonly TextBlock _badge;
        private readonly TextBlock _name;
        private readonly TextBlock _status;
        private readonly Rectangle _currentBar;
        private readonly ScaleTransform _progressScale = new() { ScaleX = 0 };
        private readonly ScaleTransform _cardScale = new() { ScaleX = 1, ScaleY = 1 };
        private readonly Func<string?, double, BitmapImage?> _loadImage;
        private readonly double _width;
        private Storyboard? _progress;

        public CardVisual(double width, double height, bool withPreview, Func<string?, double, BitmapImage?> loadImage)
        {
            _width = width;
            _loadImage = loadImage;
            Model = new CardModel(Guid.Empty, 0, string.Empty, null, false);

            _image = new Image { Stretch = Stretch.UniformToFill };
            _bigNumber = new TextBlock { FontSize = 28, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Brush("TextFillColorTertiaryBrush") };
            var previewContent = new Grid();
            previewContent.Children.Add(_bigNumber);
            previewContent.Children.Add(_image);
            _preview = new Border
            {
                Margin = new Thickness(6, 6, 6, 0),
                CornerRadius = new CornerRadius(4),
                Background = Brush("SubtleFillColorSecondaryBrush"),
                Child = previewContent,
                Visibility = withPreview ? Visibility.Visible : Visibility.Collapsed,
            };
            // Always laid out (opacity toggles) so every card has identical geometry.
            _currentBar = new Rectangle { Height = 2, Margin = new Thickness(6, 4, 6, 0), Fill = Brush("AccentFillColorDefaultBrush"), RadiusX = 1, RadiusY = 1, Opacity = 0 };

            _badge = new TextBlock { FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var badgeBorder = new Border { MinWidth = 18, Height = 18, Padding = new Thickness(4, 0, 4, 0), CornerRadius = new CornerRadius(4), Background = Brush("SubtleFillColorSecondaryBrush"), Child = _badge, VerticalAlignment = VerticalAlignment.Center };
            _name = new TextBlock { FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            var label = new Grid { ColumnSpacing = 8 };
            label.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            label.ColumnDefinitions.Add(new ColumnDefinition());
            label.Children.Add(badgeBorder);
            Grid.SetColumn(_name, 1);
            label.Children.Add(_name);
            _status = new TextBlock { FontSize = 12, Margin = new Thickness(26, 0, 0, 0), Foreground = Brush("TextFillColorSecondaryBrush"), TextTrimming = TextTrimming.CharacterEllipsis };
            var labelStack = new StackPanel { Margin = new Thickness(10, 8, 10, 10), Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
            labelStack.Children.Add(label);
            labelStack.Children.Add(_status);

            var progress = new Rectangle
            {
                Height = 3,
                VerticalAlignment = VerticalAlignment.Bottom,
                Fill = Brush("AccentFillColorDefaultBrush"),
                RenderTransform = _progressScale,
                RenderTransformOrigin = new Windows.Foundation.Point(0, 0.5),
            };

            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = withPreview ? new GridLength(1, GridUnitType.Star) : new GridLength(0) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.Children.Add(_preview);
            Grid.SetRow(_currentBar, 1);
            layout.Children.Add(_currentBar);
            Grid.SetRow(labelStack, 2);
            layout.Children.Add(labelStack);
            Grid.SetRowSpan(progress, 3);
            layout.Children.Add(progress);

            _border = new Border
            {
                Width = width,
                Height = height,
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Background = Brush("CardBackgroundFillColorDefaultBrush"),
                BorderBrush = Brush("CardStrokeColorDefaultBrush"),
                Child = layout,
                RenderTransform = _cardScale,
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
            };
            Root = _border;
        }

        public FrameworkElement Root { get; }

        public CardModel Model { get; private set; }

        public CardVisualState State { get; private set; } = CardVisualState.Rest;

        public void Bind(CardModel model, string? fallbackWallpaper = null)
        {
            Model = model;
            _badge.Text = model.Number.ToString(System.Globalization.CultureInfo.CurrentCulture);
            _bigNumber.Text = _badge.Text;
            _name.Text = model.Name;
            ToolTipService.SetToolTip(_border, model.Name);
            _image.Source = _loadImage(model.WallpaperPath ?? fallbackWallpaper, _width) ?? _image.Source;
            _currentBar.Opacity = model.IsCurrent ? 1 : 0;
            if (State == CardVisualState.Rest || State == CardVisualState.Dwelling)
            {
                _status.Text = model.IsCurrent ? "● You're here" : model.Number <= 9 ? $"Hold, or press {model.Number}" : "Hold to go here";
            }

            AutomationProperties.SetName(_border, model.IsCurrent
                ? $"Desktop {model.Number}, {model.Name}, current desktop"
                : $"Desktop {model.Number}, {model.Name}. Hold to go here");
        }

        public void SetState(CardVisualState state, TimeSpan delay, bool animate)
        {
            State = state;
            StopAnimations();
            bool highContrast = new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast;
            switch (state)
            {
                case CardVisualState.Dwelling:
                    _border.BorderThickness = new Thickness(2);
                    _border.BorderBrush = Brush(highContrast ? "SystemColorHighlightColorBrush" : "AccentFillColorDefaultBrush");
                    ScaleCard(1.04, animate);
                    _progressScale.ScaleX = 0;
                    var grow = new DoubleAnimation { From = 0, To = 1, Duration = delay };
                    Storyboard.SetTarget(grow, _progressScale);
                    Storyboard.SetTargetProperty(grow, "ScaleX");
                    _progress = new Storyboard();
                    _progress.Children.Add(grow);
                    _progress.Begin();
                    break;
                case CardVisualState.Switching:
                    _progressScale.ScaleX = 1;
                    _status.Text = "Switching…";
                    break;
                case CardVisualState.Switched:
                    _progressScale.ScaleX = 1;
                    _status.Text = "✓ Switched";
                    break;
                case CardVisualState.Error:
                    _border.BorderThickness = new Thickness(2);
                    _border.BorderBrush = Brush("SystemFillColorCriticalBrush");
                    _progressScale.ScaleX = 0;
                    _status.Text = "Couldn't switch";
                    ScaleCard(1, animate);
                    break;
                default:
                    _border.BorderThickness = new Thickness(1);
                    _border.BorderBrush = Brush("CardStrokeColorDefaultBrush");
                    _progressScale.ScaleX = 0;
                    ScaleCard(1, animate);
                    Bind(Model);
                    break;
            }
        }

        public void StopAnimations()
        {
            _progress?.Stop();
            _progress = null;
        }

        private void ScaleCard(double to, bool animate)
        {
            if (!animate)
            {
                _cardScale.ScaleX = _cardScale.ScaleY = to;
                return;
            }

            var storyboard = new Storyboard();
            foreach (string axis in new[] { "ScaleX", "ScaleY" })
            {
                var a = new DoubleAnimation { To = to, Duration = Hover };
                Storyboard.SetTarget(a, _cardScale);
                Storyboard.SetTargetProperty(a, axis);
                storyboard.Children.Add(a);
            }

            storyboard.Begin();
        }

        private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
    }

    /// <summary>Marshals coordinator calls (any thread) onto the UI thread without blocking.</summary>
    private sealed class ViewAdapter(SelectorWindow window) : ISelectorView
    {
        public void Show(IReadOnlyList<CardModel> cards, SelectorGeometry geometry, DragToSwitchSettings settings, DragKind kind) =>
            Post(() => window.ShowCore(cards, geometry, settings, kind));

        public void UpdateCards(IReadOnlyList<CardModel> cards, SelectorGeometry geometry) => Post(() => window.UpdateCardsCore(cards, geometry));

        public void SetCardState(int? index, CardVisualState state, TimeSpan delay, string? message = null) =>
            Post(() => window.SetCardStateCore(index, state, delay, message));

        public void SetCursorOverSelector(bool over) => Post(() => window.SetCursorOverSelectorCore(over));

        public void Collapse(CardModel destination) => Post(() => window.CollapseCore(destination));

        public void Expand() => Post(window.ExpandCore);

        public void Hide() => Post(window.HideCore);

        private void Post(Action action) => window.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                window._log.Error(ex, "Selector UI update failed");
            }
        });
    }
}

/// <summary>One-shot UI-thread timers that can all be cancelled together.</summary>
internal sealed class DispatcherQueueTimerHolder(Microsoft.UI.Dispatching.DispatcherQueue queue)
{
    private readonly List<Microsoft.UI.Dispatching.DispatcherQueueTimer> _timers = [];

    public void Once(TimeSpan after, Action action)
    {
        Microsoft.UI.Dispatching.DispatcherQueueTimer timer = queue.CreateTimer();
        timer.Interval = after;
        timer.IsRepeating = false;
        timer.Tick += (t, _) =>
        {
            t.Stop();
            _timers.Remove(t);
            action();
        };
        _timers.Add(timer);
        timer.Start();
    }

    public void StopAll()
    {
        foreach (Microsoft.UI.Dispatching.DispatcherQueueTimer timer in _timers)
        {
            timer.Stop();
        }

        _timers.Clear();
    }
}
