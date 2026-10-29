using FluxKit.App.Common;
using FluxKit.App.Theming;
using FluxKit.Platform.VirtualDesktop;
using FluxKit.Tools.DragToSwitch;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace FluxKit.App.Modules.DragToSwitch;

/// <summary>
/// A miniature of the desktop picker on the settings page. It shows the real desktop names and wallpapers and
/// follows the position, size, preview, theme, hold time and animation settings. Nothing here switches desktops.
/// </summary>
public sealed partial class PickerPreview : UserControl
{
    private const int MaxCards = 4;

    private readonly WallpaperCache _wallpapers = new();
    private readonly Storyboard _hold = new() { RepeatBehavior = RepeatBehavior.Forever };

    public PickerPreview()
    {
        InitializeComponent();
        Unloaded += (_, _) => _hold.Stop();
    }

    /// <summary>Raised by "Show the real picker".</summary>
    public event EventHandler? TryRequested;

    /// <summary>Redraws the miniature for these desktops, settings and picker theme.</summary>
    public void Update(IReadOnlyList<VirtualDesktopInfo> desktops, DragToSwitchSettings settings, ElementTheme pickerTheme)
    {
        Picker.RequestedTheme = pickerTheme;
        (Picker.VerticalAlignment, Picker.HorizontalAlignment) = settings.OverlayPosition switch
        {
            OverlayPosition.TopCenter => (VerticalAlignment.Top, HorizontalAlignment.Center),
            OverlayPosition.NearCursor => (VerticalAlignment.Center, HorizontalAlignment.Right),
            _ => (VerticalAlignment.Bottom, HorizontalAlignment.Center),
        };

        string? wallpaper = WallpaperCache.CurrentWallpaper();
        ScreenWallpaper.Source = _wallpapers.Load(wallpaper, 480);

        bool animate = settings.AnimationsEnabled && new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
        bool withWallpaper = settings.PreviewStyle == PreviewStyle.Wallpaper;
        double width = settings.OverlaySize == OverlaySize.Compact ? 48 : 64;
        List<(string Name, string? Wallpaper, bool IsCurrent)> cards = desktops.Count > 0
            ? desktops.Take(MaxCards).Select(d => (d.Name, d.WallpaperPath ?? wallpaper, d.IsCurrent)).ToList()
            : [(Strings.Format("Preview_DesktopFormat", 1), wallpaper, true), (Strings.Format("Preview_DesktopFormat", 2), wallpaper, false)];
        int target = Math.Max(0, cards.FindIndex(c => !c.IsCurrent));

        _hold.Stop();
        _hold.Children.Clear();
        Cards.Children.Clear();
        for (int i = 0; i < cards.Count; i++)
        {
            Cards.Children.Add(Card(cards[i], width, withWallpaper, i == target, settings.HoverDelayMs, animate));
        }

        if (animate)
        {
            _hold.Duration = TimeSpan.FromMilliseconds(settings.HoverDelayMs + 900);
            _hold.Begin();
        }

        CaptionText.Text = Strings.Format("Preview_CaptionFormat", settings.HoverDelayMs);
    }

    private Border Card((string Name, string? Wallpaper, bool IsCurrent) model, double width, bool withWallpaper, bool isTarget, int holdMs, bool animate)
    {
        double height = Math.Round(width * 0.62);
        var face = new Grid { Width = width, Height = height, Background = ThemeResources.SubtleFill };
        if (withWallpaper && _wallpapers.Load(model.Wallpaper, 160) is { } image)
        {
            face.Children.Add(new Image { Source = image, Stretch = Stretch.UniformToFill });
        }
        else
        {
            face.Children.Add(new TextBlock
            {
                Text = model.Name,
                FontSize = 9,
                Margin = new Thickness(4),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }

        if (isTarget)
        {
            // The hold bar fills over the configured delay, like the real picker's; without animations it rests part-way.
            var scale = new ScaleTransform { ScaleX = animate ? 0 : 0.6 };
            face.Children.Add(new Border
            {
                Height = 3,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = ThemeResources.BrandGradient,
                RenderTransform = scale,
                RenderTransformOrigin = new Windows.Foundation.Point(0, 0.5),
            });
            var fill = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(holdMs) };
            Storyboard.SetTarget(fill, scale);
            Storyboard.SetTargetProperty(fill, "ScaleX");
            _hold.Children.Add(fill);
        }

        var stack = new StackPanel();
        stack.Children.Add(new Border { CornerRadius = new CornerRadius(4), Child = face });
        stack.Children.Add(new TextBlock
        {
            Text = model.Name,
            FontSize = 8,
            MaxWidth = width,
            Margin = new Thickness(0, 3, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        return new Border
        {
            Padding = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1.5),
            BorderBrush = isTarget ? ThemeResources.BrandGradient : model.IsCurrent ? ThemeResources.Accent : null,
            Child = stack,
        };
    }

    private void OnTryClick(object sender, RoutedEventArgs e) => TryRequested?.Invoke(this, EventArgs.Empty);
}
