using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media.Imaging;

namespace FluxKit.App.Controls;

/// <summary>
/// The header band of a page. Tool pages put their on/off switch in <see cref="Trailing"/> and show the tool's
/// state in <see cref="StatusText"/>; other pages use only the icon, title and description.
/// </summary>
[ContentProperty(Name = nameof(Trailing))]
public sealed partial class PageHeader : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty, OnDescriptionChanged));

    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty, OnIconChanged));

    public static readonly DependencyProperty IconAssetProperty =
        DependencyProperty.Register(nameof(IconAsset), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty, OnIconChanged));

    public static readonly DependencyProperty StatusTextProperty =
        DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty, OnStatusChanged));

    public static readonly DependencyProperty StatusToneProperty =
        DependencyProperty.Register(nameof(StatusTone), typeof(StatusTone), typeof(PageHeader), new PropertyMetadata(StatusTone.None, OnStatusChanged));

    public static readonly DependencyProperty TrailingProperty =
        DependencyProperty.Register(nameof(Trailing), typeof(object), typeof(PageHeader), new PropertyMetadata(null));

    public PageHeader()
    {
        InitializeComponent();
        AutomationProperties.SetHeadingLevel(TitleElement, AutomationHeadingLevel.Level1);
        UpdateIcon();
        UpdateStatus();
        DescriptionElement.Visibility = Visibility.Collapsed;
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>Segoe Fluent Icons glyph, used when <see cref="IconAsset"/> is empty.</summary>
    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    /// <summary>A file name under Assets, e.g. "DragToSwitch.png".</summary>
    public string IconAsset
    {
        get => (string)GetValue(IconAssetProperty);
        set => SetValue(IconAssetProperty, value);
    }

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public StatusTone StatusTone
    {
        get => (StatusTone)GetValue(StatusToneProperty);
        set => SetValue(StatusToneProperty, value);
    }

    public object? Trailing
    {
        get => GetValue(TrailingProperty);
        set => SetValue(TrailingProperty, value);
    }

    private void UpdateIcon()
    {
        bool hasAsset = !string.IsNullOrEmpty(IconAsset);
        IconImage.Source = hasAsset ? new BitmapImage(new Uri($"ms-appx:///Assets/{IconAsset}")) : null;
        IconImage.Visibility = hasAsset ? Visibility.Visible : Visibility.Collapsed;
        IconGlyph.Visibility = hasAsset ? Visibility.Collapsed : Visibility.Visible;
        Squircle.Visibility = hasAsset || !string.IsNullOrEmpty(Glyph) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateStatus()
    {
        StatusPanel.Visibility = string.IsNullOrEmpty(StatusText) ? Visibility.Collapsed : Visibility.Visible;
        StatusDot.Fill = StatusTones.Brush(StatusTone);
        StatusDot.Visibility = StatusDot.Fill is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((PageHeader)d).UpdateIcon();

    private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((PageHeader)d).UpdateStatus();

    private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((PageHeader)d).DescriptionElement.Visibility = string.IsNullOrEmpty(e.NewValue as string) ? Visibility.Collapsed : Visibility.Visible;
}
