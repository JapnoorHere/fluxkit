using FluxKit.App.Theming;
using FluxKit.Utilities.DragToSwitch.Interaction;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;

namespace FluxKit.App.Modules.DragToSwitch;

/// <summary>Visual tree and state for one desktop card.</summary>
internal sealed class DesktopCardView
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

    public DesktopCardView(double width, double height, bool withPreview, Func<string?, double, BitmapImage?> loadImage)
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
            var a = new DoubleAnimation { To = to, Duration = Motion.Hover };
            Storyboard.SetTarget(a, _cardScale);
            Storyboard.SetTargetProperty(a, axis);
            storyboard.Children.Add(a);
        }

        storyboard.Begin();
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
