using FluxKit.App.Modules.FluxIsland.Design;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>A small button on a list row, such as remove or pin.</summary>
internal sealed record ListAction(string Glyph, string Name, Action Run, bool Active = false);

/// <summary>
/// One row of a list page. <see cref="Key"/> identifies it; <see cref="Open"/> runs when the row is clicked.
/// </summary>
internal sealed record ListItem(string Key, string Text, string? Caption, string? Glyph, Color Accent, Action? Open, string? OpenName, IReadOnlyList<ListAction> Actions);

/// <summary>What a list page shows and how: set once by the pill with <see cref="ListPanel.Configure"/>.</summary>
internal sealed record ListPageSpec(string Glyph, Color Accent, string TitleKey, string NameFormatKey, Func<IslandScene, DateTimeOffset, IReadOnlyList<ListItem>> Items, Action Clear);

/// <summary>
/// The list page used for notes, recent moments (Rewind) and clipboard history: a header with a count and Clear,
/// then up to a few rows at a time in a scroller. Rows are rebuilt only when the list actually changes.
/// </summary>
public sealed partial class ListPanel : UserControl, IIslandPage
{
    private const double RowHeight = 36;
    private const int VisibleRows = 3;

    private ListPageSpec? _spec;
    private string _signature = string.Empty;
    private int _count;

    public ListPanel()
    {
        InitializeComponent();
        Count.Foreground = new SolidColorBrush(IslandPalette.TextSecondary);
        ClearButton.Click += (_, _) => _spec?.Clear();
        PressFeedback.Attach(ClearButton);
    }

    UIElement[] IIslandPage.Rows => [Header, ItemsHost];

    Size IIslandPage.PreferredSize => new(420, 52 + (RowHeight * Math.Clamp(_count, 1, VisibleRows)));

    string IIslandPage.State => "list";

    string IIslandPage.Update(IslandScene scene, DateTimeOffset now, bool springs)
    {
        if (_spec is null)
        {
            return string.Empty;
        }

        IReadOnlyList<ListItem> items = _spec.Items(scene, now);
        string signature = string.Join('\n', items.Select(i => $"{i.Key}|{i.Text}|{i.Caption}|{string.Join(',', i.Actions.Select(a => a.Active))}"));
        if (signature != _signature)
        {
            _signature = signature;
            Items.Children.Clear();
            foreach (ListItem item in items)
            {
                Items.Children.Add(CreateRow(item));
            }
        }

        _count = items.Count;
        Count.Text = _count.ToString(System.Globalization.CultureInfo.CurrentCulture);
        ClearButton.Visibility = _count > 0 ? Visibility.Visible : Visibility.Collapsed;
        return Strings.Format(_spec.NameFormatKey, _count);
    }

    internal void Configure(ListPageSpec spec)
    {
        _spec = spec;
        HeaderGlyph.Show(spec.Glyph, spec.Accent);
        Title.Text = Strings.Get(spec.TitleKey);
    }

    private Grid CreateRow(ListItem item)
    {
        var row = new Grid { Height = RowHeight, ColumnSpacing = 6 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        if (item.Glyph is { } glyph)
        {
            content.Children.Add(new FontIcon { Glyph = glyph, FontSize = 13, Foreground = new SolidColorBrush(item.Accent) });
        }

        content.Children.Add(new TextBlock { Text = item.Text, FontSize = 13, MaxLines = 1, MaxWidth = 250, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
        var open = new Button
        {
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Style = (Style)Resources["IslandChipButtonStyle"],
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            FontWeight = Microsoft.UI.Text.FontWeights.Normal,
            Padding = new Thickness(8, 0, 8, 0),
            IsEnabled = item.Open is not null,
        };
        if (item.OpenName is { } openName)
        {
            ToolTipService.SetToolTip(open, openName);
            AutomationProperties.SetName(open, $"{item.Text}, {openName}");
        }

        open.Click += (_, _) => item.Open?.Invoke();
        PressFeedback.Attach(open);
        row.Children.Add(open);

        var trailing = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        if (item.Caption is { } caption)
        {
            trailing.Children.Add(new TextBlock
            {
                Text = caption,
                FontSize = 11,
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(IslandPalette.TextTertiary),
            });
        }

        foreach (ListAction action in item.Actions)
        {
            var button = new Button
            {
                Content = new FontIcon { Glyph = action.Glyph, FontSize = 11, Foreground = new SolidColorBrush(action.Active ? IslandPalette.Yellow : IslandPalette.TextSecondary) },
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Style = (Style)Resources["IslandIconButtonStyle"],
            };
            AutomationProperties.SetName(button, action.Name);
            ToolTipService.SetToolTip(button, action.Name);
            button.Click += (_, _) => action.Run();
            trailing.Children.Add(button);
        }

        Grid.SetColumn(trailing, 1);
        row.Children.Add(trailing);
        return row;
    }

    /// <summary>"now", "5 min", "3 h" or the date, for how long ago something happened.</summary>
    internal static string Age(DateTimeOffset at, DateTimeOffset now)
    {
        TimeSpan age = now - at;
        return age.TotalMinutes < 1 ? Strings.Get("Island_AgeNow")
            : age.TotalHours < 1 ? Strings.Format("Island_MinutesFormat", (int)age.TotalMinutes)
            : age.TotalDays < 1 ? Strings.Format("Island_AgeHoursFormat", (int)age.TotalHours)
            : at.ToLocalTime().ToString("d MMM", System.Globalization.CultureInfo.CurrentCulture);
    }
}
