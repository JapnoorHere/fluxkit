using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;

namespace FluxKit.App.Controls;

/// <summary>
/// The slim navigation rail of the main window: an icon over a short caption per page, with a status dot for
/// tools. Items are identified by a tag ("home", a tool id, "general", "about").
/// </summary>
public sealed partial class NavRail : UserControl
{
    private readonly Dictionary<string, (RadioButton Button, Ellipse Dot)> _items = [];

    public NavRail() => InitializeComponent();

    /// <summary>Raised when an item becomes selected, by the user or by <see cref="Select"/>; the argument is its tag.</summary>
    public event EventHandler<string>? ItemInvoked;

    public void AddItem(string tag, string caption, string fullName, IconElement icon, bool atBottom = false)
    {
        icon.Width = 20;
        icon.Height = 20;
        var dot = new Ellipse
        {
            Width = 8,
            Height = 8,
            Margin = new Thickness(0, -2, -4, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Collapsed,
        };
        var iconHost = new Grid { Width = 24, Height = 24, HorizontalAlignment = HorizontalAlignment.Center };
        iconHost.Children.Add(icon);
        iconHost.Children.Add(dot);

        var content = new StackPanel { Spacing = 4 };
        content.Children.Add(iconHost);
        content.Children.Add(new TextBlock
        {
            Text = caption,
            FontSize = 11,
            MaxWidth = 60,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 2,
        });

        var button = new RadioButton
        {
            Content = content,
            Tag = tag,
            GroupName = "FluxKitRail",
            Style = (Style)Application.Current.Resources["RailItemStyle"],
        };
        ToolTipService.SetToolTip(button, fullName);
        AutomationProperties.SetName(button, fullName);
        button.Checked += (_, _) => ItemInvoked?.Invoke(this, tag);
        _items[tag] = (button, dot);
        (atBottom ? BottomItems : TopItems).Children.Add(button);
    }

    /// <summary>Selects the item with this tag; raises <see cref="ItemInvoked"/> even if it was already selected.</summary>
    public void Select(string tag)
    {
        if (!_items.TryGetValue(tag, out (RadioButton Button, Ellipse Dot) item))
        {
            return;
        }

        if (item.Button.IsChecked == true)
        {
            ItemInvoked?.Invoke(this, tag);
        }
        else
        {
            item.Button.IsChecked = true; // raises ItemInvoked through Checked
        }
    }

    public void SetStatus(string tag, StatusTone tone, string statusName)
    {
        if (!_items.TryGetValue(tag, out (RadioButton Button, Ellipse Dot) item))
        {
            return;
        }

        item.Dot.Fill = StatusTones.Brush(tone);
        item.Dot.Visibility = item.Dot.Fill is null ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetItemStatus(item.Button, statusName);
    }
}
