using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace FluxKit.App.Controls;

/// <summary>A tool on the Home launchpad: icon, name, description, status and an on/off switch.</summary>
public sealed partial class ToolTile : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ToolTile), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(ToolTile), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty StatusTextProperty =
        DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(ToolTile), new PropertyMetadata(string.Empty));

    private bool _settingToggle;

    public ToolTile() => InitializeComponent();

    /// <summary>Raised when the card is clicked (open the tool).</summary>
    public event EventHandler? Opened;

    /// <summary>Raised when the user flips the switch; the argument is the new state.</summary>
    public event EventHandler<bool>? EnabledChanged;

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

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public bool IsOn
    {
        get => Toggle.IsOn;
        set
        {
            _settingToggle = true;
            Toggle.IsOn = value;
            _settingToggle = false;
        }
    }

    public void SetStatusTone(StatusTone tone)
    {
        StatusDot.Fill = StatusTones.Brush(tone);
        StatusDot.Visibility = StatusDot.Fill is null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Shows a Fluent glyph, or the colour icon under Assets when <paramref name="asset"/> is set.</summary>
    public void SetIcon(string glyph, string? asset)
    {
        IconImage.Source = asset is null ? null : new BitmapImage(new Uri($"ms-appx:///Assets/{asset}"));
        IconImage.Visibility = asset is null ? Visibility.Collapsed : Visibility.Visible;
        IconGlyph.Glyph = glyph;
        IconGlyph.Visibility = asset is null ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Names the two controls for screen readers, e.g. "Open Drag-to-Switch" and "Turn on Drag-to-Switch".</summary>
    public void SetAccessibleNames(string open, string toggle)
    {
        AutomationProperties.SetName(OpenButton, open);
        AutomationProperties.SetName(Toggle, toggle);
    }

    /// <summary>Turns the tile into the dashed "more tools" placeholder: no button, no switch.</summary>
    public void MakePlaceholder()
    {
        OpenButton.Visibility = Visibility.Collapsed;
        Toggle.Visibility = Visibility.Collapsed;
        PlaceholderRoot.Visibility = Visibility.Visible;
    }

    private void OnOpenClick(object sender, RoutedEventArgs e) => Opened?.Invoke(this, EventArgs.Empty);

    private void OnToggled(object sender, RoutedEventArgs e)
    {
        if (!_settingToggle)
        {
            EnabledChanged?.Invoke(this, Toggle.IsOn);
        }
    }
}
