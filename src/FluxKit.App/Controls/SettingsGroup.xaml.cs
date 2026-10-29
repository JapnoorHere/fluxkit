using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace FluxKit.App.Controls;

/// <summary>
/// A titled card that holds a set of <see cref="SettingRow"/>s (or any content). The content is usually a
/// StackPanel of rows; the first row's divider is hidden so the card starts cleanly under its title.
/// </summary>
[ContentProperty(Name = nameof(Body))]
public sealed partial class SettingsGroup : UserControl
{
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(SettingsGroup), new PropertyMetadata(string.Empty, OnHeaderChanged));

    public static readonly DependencyProperty CaptionProperty =
        DependencyProperty.Register(nameof(Caption), typeof(string), typeof(SettingsGroup), new PropertyMetadata(string.Empty, OnCaptionChanged));

    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(SettingsGroup), new PropertyMetadata(string.Empty, OnGlyphChanged));

    public static readonly DependencyProperty BodyProperty =
        DependencyProperty.Register(nameof(Body), typeof(object), typeof(SettingsGroup), new PropertyMetadata(null));

    public SettingsGroup()
    {
        InitializeComponent();
        CaptionElement.Visibility = Visibility.Collapsed;
        GlyphElement.Visibility = Visibility.Collapsed;
        Loaded += (_, _) => HideFirstDivider();
    }

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public object? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    private void HideFirstDivider()
    {
        if (Body is Panel panel)
        {
            bool first = true;
            foreach (SettingRow row in panel.Children.OfType<SettingRow>())
            {
                row.ShowDivider = !first;
                first = false;
            }
        }
    }

    // Screen readers announce the group by its title.
    private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var group = (SettingsGroup)d;
        AutomationProperties.SetName(group, e.NewValue as string ?? string.Empty);
        AutomationProperties.SetHeadingLevel(group.HeaderPanel, AutomationHeadingLevel.Level2);
    }

    private static void OnCaptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((SettingsGroup)d).CaptionElement.Visibility = string.IsNullOrEmpty(e.NewValue as string) ? Visibility.Collapsed : Visibility.Visible;

    private static void OnGlyphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((SettingsGroup)d).GlyphElement.Visibility = string.IsNullOrEmpty(e.NewValue as string) ? Visibility.Collapsed : Visibility.Visible;
}
