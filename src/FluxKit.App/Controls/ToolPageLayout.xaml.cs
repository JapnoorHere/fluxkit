using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace FluxKit.App.Controls;

/// <summary>
/// The body of a tool page: a live preview next to the tool's settings groups. Below
/// <see cref="NarrowWidth"/> the preview moves above the settings.
/// </summary>
[ContentProperty(Name = nameof(Settings))]
public sealed partial class ToolPageLayout : UserControl
{
    public const double NarrowWidth = 820;

    public static readonly DependencyProperty PreviewProperty =
        DependencyProperty.Register(nameof(Preview), typeof(UIElement), typeof(ToolPageLayout), new PropertyMetadata(null, OnPreviewChanged));

    public static readonly DependencyProperty SettingsProperty =
        DependencyProperty.Register(nameof(Settings), typeof(object), typeof(ToolPageLayout), new PropertyMetadata(null));

    private bool? _narrow;

    public ToolPageLayout()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Arrange(e.NewSize.Width < NarrowWidth);
    }

    public UIElement? Preview
    {
        get => (UIElement?)GetValue(PreviewProperty);
        set => SetValue(PreviewProperty, value);
    }

    public object? Settings
    {
        get => GetValue(SettingsProperty);
        set => SetValue(SettingsProperty, value);
    }

    // An element can have only one parent, so it is moved between the two hosts rather than shown twice.
    private void Arrange(bool narrow, bool force = false)
    {
        if (_narrow == narrow && !force)
        {
            return;
        }

        _narrow = narrow;
        WidePreviewHost.Content = null;
        NarrowPreviewHost.Content = null;
        if (narrow)
        {
            NarrowPreviewHost.Content = Preview;
        }
        else
        {
            WidePreviewHost.Content = Preview;
        }

        RootGrid.ColumnSpacing = narrow ? 0 : 24;
        PreviewScroller.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
        NarrowPreviewHost.Visibility = narrow ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void OnPreviewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var layout = (ToolPageLayout)d;
        layout.Arrange(layout._narrow ?? false, force: true);
    }
}
