using FluxKit.App.Common;
using FluxKit.App.Theming;
using FluxKit.Platform.VirtualDesktop;
using FluxKit.Platform.VirtualDesktop.Interop;
using FluxKit.Platform.Windows;
using FluxKit.Tools.DragToSwitch;
using FluxKit.Tools.DragToSwitch.Interaction;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
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
    private readonly IVirtualDesktopService _desktops;
    private readonly ILogger _log;
    private readonly nint _hwnd;
    private readonly List<DesktopCardView> _cards = [];
    private readonly WallpaperCache _wallpapers = new();
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
        Instruction.Text = Strings.Get("Picker_PreviewInstruction");
        int version = _showVersion;
        _timers.Once(TimeSpan.FromSeconds(4), () =>
        {
            if (version == _showVersion)
            {
                HideCore();
            }
        });
    }

    private string HoldText => Strings.Get(_kind == DragKind.Window ? "Picker_HoldWindow" : "Picker_HoldFile");

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
            Motion.Animate(Root, "Opacity", 0, 1, Motion.Entrance, decelerate: true);
            Motion.Animate(RootTranslate, "Y", from, 0, Motion.Entrance, decelerate: true);
            for (int i = 0; i < _cards.Count && i < 8; i++)
            {
                _cards[i].Root.Opacity = 0;
                Motion.Animate(_cards[i].Root, "Opacity", 0, 1, Motion.Entrance, decelerate: true, beginAfter: TimeSpan.FromMilliseconds(20 * i));
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

        DesktopCardView card = _cards[i];
        card.SetState(state, delay, AnimationsOn);
        switch (state)
        {
            case CardVisualState.Switching:
                Instruction.Text = Strings.Format("Picker_SwitchingFormat", card.Model.Name);
                break;
            case CardVisualState.Switched:
                Instruction.Text = _kind == DragKind.Window
                    ? Strings.Format("Picker_NowOnWindowFormat", card.Model.Name)
                    : Strings.Format("Picker_NowOnFileFormat", card.Model.Name);
                Announce(Strings.Format("Picker_SwitchedFormat", card.Model.Name));
                break;
            case CardVisualState.Error:
                Instruction.Text = message ?? Strings.Get("Picker_SwitchFailed");
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
            Instruction.Text = over && _kind == DragKind.File ? Strings.Get("Picker_DropOutside") : HoldText;
        }
    }

    private void CollapseCore(CardModel destination)
    {
        if (_geometry is null)
        {
            return;
        }

        PillText.Text = _kind == DragKind.Window
            ? Strings.Format("Picker_OnWindowFormat", destination.Name)
            : Strings.Format("Picker_OnFileFormat", destination.Name);
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
                    Motion.Animate(Root, "Opacity", 1, 0.7, TimeSpan.FromMilliseconds(250), decelerate: true);
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
        foreach (DesktopCardView card in _cards)
        {
            card.SetState(CardVisualState.Rest, TimeSpan.Zero, animate: false);
        }

        Place(_geometry.Overlay);
        if (AnimationsOn)
        {
            Motion.Animate(Root, "Opacity", 0.6, 1, Motion.Entrance, decelerate: true);
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
        foreach (DesktopCardView card in _cards)
        {
            card.StopAnimations();
        }

        if (AnimationsOn && Expanded.Visibility == Visibility.Visible)
        {
            Motion.Animate(Root, "Opacity", Root.Opacity, 0, Motion.Exit, decelerate: false);
            Motion.Animate(RootTranslate, "Y", 0, 8, Motion.Exit, decelerate: false);
            _timers.Once(Motion.Exit, () =>
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
        foreach (DesktopCardView old in _cards)
        {
            old.StopAnimations();
        }

        _cards.Clear();
        CardsPanel.Children.Clear();
        double scale = geometry.Scale;
        string? fallbackWallpaper = _settings.PreviewStyle == PreviewStyle.Wallpaper ? WallpaperCache.CurrentWallpaper() : null;
        for (int i = 0; i < cards.Count; i++)
        {
            PixelRect rect = geometry.Cards[i];
            var visual = new DesktopCardView(rect.Width / scale, rect.Height / scale, _settings.PreviewStyle != PreviewStyle.None, (path, widthDip) => _wallpapers.Load(path, (int)(widthDip * (_geometry?.Scale ?? 1))));
            visual.Bind(cards[i], fallbackWallpaper);
            _cards.Add(visual);
            CardsPanel.Children.Add(visual.Root);
        }
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
        e.DragUIOverride.Caption = Strings.Get("Picker_DropCaption");
        e.DragUIOverride.IsGlyphVisible = true;
    }
}
