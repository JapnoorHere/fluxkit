using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace FluxKit.App.Theming;

/// <summary>
/// Typed access to the theme brushes and styles that code-behind needs, so key strings live in one place.
/// Brushes come from the app resources, so they follow the app theme; XAML should use {ThemeResource} instead.
/// </summary>
internal static class ThemeResources
{
    public static Brush TextSecondary => Brush("TextFillColorSecondaryBrush");

    public static Brush TextTertiary => Brush("TextFillColorTertiaryBrush");

    public static Brush Accent => Brush("AccentFillColorDefaultBrush");

    public static Brush CardBackground => Brush("CardBackgroundFillColorDefaultBrush");

    public static Brush CardStroke => Brush("CardStrokeColorDefaultBrush");

    public static Brush SubtleFill => Brush("SubtleFillColorSecondaryBrush");

    public static Brush Critical => Brush("SystemFillColorCriticalBrush");

    public static Brush Success => Brush("SystemFillColorSuccessBrush");

    public static Brush Caution => Brush("SystemFillColorCautionBrush");

    public static Brush Neutral => Brush("SystemFillColorNeutralBrush");

    public static Brush HighContrastHighlight => Brush("SystemColorHighlightColorBrush");

    public static Brush BrandGradient => Brush("BrandGradientBrush");

    public static Style CaptionText => Style("CaptionTextBlockStyle");

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

    private static Style Style(string key) => (Style)Application.Current.Resources[key];
}
