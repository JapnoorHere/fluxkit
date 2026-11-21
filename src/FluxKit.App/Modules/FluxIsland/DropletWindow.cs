using System.Numerics;
using FluxKit.App.Modules.FluxIsland.Design;
using FluxKit.App.Theming;
using FluxKit.Platform.Windows;
using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// A torn-off activity floating on its own (plan A): a small drop of the island holding music, a timer or a stopwatch.
/// Click it to play/pause or pause/resume. Drag it anywhere; drop it back near the island to dock, or right-click it.
/// It ticks once a second only while it shows something counting.
/// </summary>
internal sealed class DropletWindow
{
    /// <summary>The droplet's size in view pixels, and the margin around it for its shadow.</summary>
    public const double DropletWidth = 210;

    public const double DropletHeight = 46;

    private const double Margin = 8;

    private readonly Window _window = new();
    private readonly nint _hwnd;
    private readonly Border _drop;
    private readonly IconBadge _badge = new() { BadgeSize = 28 };
    private readonly TextBlock _title = new() { FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, MaxLines = 1, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _trail = new() { FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(8, 0, 0, 0) };
    private readonly DispatcherQueueTimer _tick;
    private readonly ActivityKind _kind;
    private readonly Func<IIslandCommands?> _commands;
    private readonly Func<(int CentreX, int Bottom)> _islandDock;
    private IslandScene _scene = new();
    private double _scale = 1;
    private (int X, int Y) _place;
    private (int X, int Y)? _grab;
    private bool _dragged;

    /// <param name="kind">What this droplet holds.</param>
    /// <param name="commands">The island's commands (null in previews).</param>
    /// <param name="islandDock">Where the island's dock is on screen: its centre and bottom, in pixels.</param>
    public DropletWindow(ActivityKind kind, Func<IIslandCommands?> commands, Func<(int CentreX, int Bottom)> islandDock)
    {
        _kind = kind;
        _commands = commands;
        _islandDock = islandDock;
        _title.Foreground = new SolidColorBrush(IslandPalette.TextPrimary);
        _trail.Foreground = new SolidColorBrush(IslandPalette.TextSecondary);
        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_trail, FontNumeralAlignment.Tabular);

        var content = new Grid { ColumnSpacing = 10, VerticalAlignment = VerticalAlignment.Center };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.Children.Add(_badge);
        Grid.SetColumn(_title, 1);
        _title.VerticalAlignment = VerticalAlignment.Center;
        content.Children.Add(_title);
        Grid.SetColumn(_trail, 2);
        _trail.VerticalAlignment = VerticalAlignment.Center;
        content.Children.Add(_trail);

        _drop = new Border
        {
            Width = DropletWidth,
            Height = DropletHeight,
            CornerRadius = new CornerRadius(DropletHeight / 2),
            Background = new SolidColorBrush(IslandPalette.Surface),
            BorderBrush = new SolidColorBrush(IslandPalette.Highlight),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(9, 0, 16, 0),
            Child = content,
            Margin = new Thickness(Margin),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        AutomationProperties.SetName(_drop, Strings.Get("Island_DropletName"));
        ToolTipService.SetToolTip(_drop, Strings.Get("Island_DropletHelp"));
        _drop.PointerPressed += OnPressed;
        _drop.PointerMoved += OnMoved;
        _drop.PointerReleased += OnReleased;
        _drop.RightTapped += (_, e) =>
        {
            e.Handled = true;
            Dock();
        };
        _drop.PointerEntered += (_, _) => ScaleTo(1.04f);
        _drop.PointerExited += (_, _) => ScaleTo(1);

        _window.Content = new Grid { Children = { _drop } };
        _window.SystemBackdrop = new TransparentBackdrop();
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        _window.AppWindow.IsShownInSwitchers = false;
        if (_window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsResizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        WindowHelpers.MakeOverlay(_hwnd);
        WindowHelpers.RemoveFrame(_hwnd);
        _tick = _drop.DispatcherQueue.CreateTimer();
        _tick.Interval = TimeSpan.FromSeconds(1);
        _tick.Tick += (_, _) => Fill(DateTimeOffset.Now);
    }

    /// <summary>Shows or updates the droplet at <paramref name="place"/> with what <paramref name="scene"/> says now.</summary>
    public void Show(IslandScene scene, DropletPlace place, bool springs)
    {
        _scene = scene;
        bool first = !_window.Visible;
        if (_grab is null && (first || (place.X, place.Y) != _place))
        {
            _place = (place.X, place.Y);
            Place();
        }

        Fill(DateTimeOffset.Now);
        bool counting = scene.Timer?.State == TimerState.Running || scene.Stopwatch?.State == TimerState.Running;
        if (counting)
        {
            _tick.Start();
        }
        else
        {
            _tick.Stop();
        }

        if (first)
        {
            IslandMotion.Pop(_drop, springs);
        }
    }

    public void Close()
    {
        _tick.Stop();
        _window.Close();
    }

    /// <summary>The activity as the pill would show it: its icon, its name, and its running value.</summary>
    private void Fill(DateTimeOffset now)
    {
        IslandActivity? activity = _kind switch
        {
            ActivityKind.Media when _scene.Media is { } media => new IslandActivity(ActivityKind.Media) { Media = media },
            ActivityKind.Timer when _scene.Timer is { IsActive: true } timer => new IslandActivity(ActivityKind.Timer) { Timer = timer },
            ActivityKind.Stopwatch when _scene.Stopwatch is { IsActive: true } stopwatch => new IslandActivity(ActivityKind.Stopwatch) { Stopwatch = stopwatch },
            _ => null,
        };
        if (activity is null)
        {
            return;
        }

        CompactContent content = IslandPresenter.Describe(activity, now);
        _badge.Show(content.Glyph, content.Accent);
        _title.Text = content.Title.Length > 0 ? content.Title : content.Subtitle ?? string.Empty;
        _trail.Text = content.Trail ?? string.Empty;
        _trail.Foreground = new SolidColorBrush(content.Accent == IslandPalette.TextPrimary ? IslandPalette.TextSecondary : content.Accent);
    }

    private void Place()
    {
        _scale = _drop.XamlRoot?.RasterizationScale ?? WindowHelpers.GetDpi(_hwnd) / 96.0;
        int width = (int)Math.Ceiling((DropletWidth + (Margin * 2)) * _scale);
        int height = (int)Math.Ceiling((DropletHeight + (Margin * 2)) * _scale);
        var bounds = new PixelRect(_place.X, _place.Y, _place.X + width, _place.Y + height);
        _window.AppWindow.MoveAndResize(new RectInt32(bounds.Left, bounds.Top, width, height));
        int inset = (int)Math.Round(Margin * _scale);
        WindowHelpers.SetHitRegion(_hwnd, new PixelRect(inset, inset, width - inset, height - inset), (int)Math.Ceiling(DropletHeight / 2 * _scale));
        WindowHelpers.ShowNoActivate(_hwnd, bounds);
        WindowHelpers.RemoveBorder(_hwnd);
    }

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(_drop).Properties.IsLeftButtonPressed)
        {
            return;
        }

        (int x, int y) = WindowHelpers.GetCursorPosition();
        _grab = (x - _place.X, y - _place.Y);
        _dragged = false;
        _drop.CapturePointer(e.Pointer);
        ScaleTo(0.97f);
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_grab is not { } grab)
        {
            return;
        }

        (int x, int y) = WindowHelpers.GetCursorPosition();
        (int X, int Y) next = (x - grab.X, y - grab.Y);
        if (!_dragged && Math.Abs(next.X - _place.X) + Math.Abs(next.Y - _place.Y) < WindowHelpers.DragThresholdPixels())
        {
            return;
        }

        _dragged = true;
        _place = next;
        _window.AppWindow.Move(new PointInt32(next.X, next.Y));
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_grab is null)
        {
            return;
        }

        _grab = null;
        _drop.ReleasePointerCapture(e.Pointer);
        ScaleTo(1);
        if (!_dragged)
        {
            Toggle();
            return;
        }

        // Dropped near the island: it flows back in.
        (int centreX, int bottom) = _islandDock();
        double dropCentreX = _place.X + ((DropletWidth / 2) + Margin) * _scale;
        bool nearIsland = Math.Abs(dropCentreX - centreX) < 160 * _scale && _place.Y < bottom + (60 * _scale);
        if (nearIsland)
        {
            Dock();
        }
        else
        {
            _commands()?.MoveDroplet(_kind, _place.X, _place.Y);
        }
    }

    /// <summary>A click: play or pause the music, or pause or resume the timer or stopwatch.</summary>
    private void Toggle()
    {
        IIslandCommands? commands = _commands();
        switch (_kind)
        {
            case ActivityKind.Media:
                commands?.PlayPause();
                break;
            case ActivityKind.Timer:
                commands?.PauseOrResumeTimer();
                break;
            case ActivityKind.Stopwatch:
                commands?.PauseOrResumeStopwatch();
                break;
        }

        IslandMotion.Pop(_badge, springs: true);
    }

    private void Dock() => _commands()?.DockActivity(_kind);

    private void ScaleTo(float scale)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(_drop);
        visual.CenterPoint = new Vector3((float)DropletWidth / 2, (float)DropletHeight / 2, 0);
        visual.StartAnimation("Scale", IslandSprings.Vector3(visual.Compositor, IslandSprings.Snappy, new Vector3(scale, scale, 1)));
    }
}
