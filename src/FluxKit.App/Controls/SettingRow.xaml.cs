using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace FluxKit.App.Controls;

/// <summary>Settings card used on every FluxKit page. The trailing control is the element's content.</summary>
[ContentProperty(Name = nameof(Action))]
public sealed partial class SettingRow : UserControl
{
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(SettingRow), new PropertyMetadata(string.Empty, OnHeaderChanged));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingRow), new PropertyMetadata(string.Empty, OnDescriptionChanged));

    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(SettingRow), new PropertyMetadata(string.Empty, OnGlyphChanged));

    public static readonly DependencyProperty ActionProperty =
        DependencyProperty.Register(nameof(Action), typeof(object), typeof(SettingRow), new PropertyMetadata(null, OnHeaderChanged));

    public SettingRow()
    {
        InitializeComponent();
        DescriptionElement.Visibility = Visibility.Collapsed;
        IconElement.Visibility = Visibility.Collapsed;
    }

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public object? Action
    {
        get => GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }

    // The trailing control is named after the row so screen readers announce e.g. "Hover delay, slider".
    private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var row = (SettingRow)d;
        if (row.Action is DependencyObject control && string.IsNullOrEmpty(AutomationProperties.GetName(control)))
        {
            AutomationProperties.SetName(control, row.Header);
        }
    }

    private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((SettingRow)d).DescriptionElement.Visibility = string.IsNullOrEmpty(e.NewValue as string) ? Visibility.Collapsed : Visibility.Visible;

    private static void OnGlyphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((SettingRow)d).IconElement.Visibility = string.IsNullOrEmpty(e.NewValue as string) ? Visibility.Collapsed : Visibility.Visible;
}
