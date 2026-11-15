using FluxKit.App.Modules.FluxIsland.Design;
using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// The notes page. Rows are rebuilt only when the notes change (there are at most 20), so updates while the panel
/// is open cost nothing. Clicking a note copies it; the × removes it.
/// </summary>
public sealed partial class NotesPanel : UserControl, IIslandPage
{
    private const double RowHeight = 36;
    private const int VisibleRows = 3;
    private IReadOnlyList<IslandNote> _shown = [];

    public NotesPanel()
    {
        InitializeComponent();
        HeaderGlyph.Foreground = new SolidColorBrush(IslandPalette.Yellow);
        Count.Foreground = new SolidColorBrush(IslandPalette.TextSecondary);
        ClearButton.Click += (_, _) => Commands?.ClearNotes();
        PressFeedback.Attach(ClearButton);
    }

    public IIslandCommands? Commands { get; set; }

    UIElement[] IIslandPage.Rows => [Header, ItemsHost];

    Size IIslandPage.PreferredSize => new(420, 52 + (RowHeight * Math.Clamp(_shown.Count, 1, VisibleRows)));

    string IIslandPage.State => "notes";

    string IIslandPage.Update(IslandScene scene, DateTimeOffset now, bool springs)
    {
        if (!ReferenceEquals(scene.Notes, _shown) && !scene.Notes.SequenceEqual(_shown))
        {
            _shown = scene.Notes;
            Rebuild(now);
        }

        Count.Text = _shown.Count.ToString(System.Globalization.CultureInfo.CurrentCulture);
        return Strings.Format("Island_NotesNameFormat", _shown.Count);
    }

    private void Rebuild(DateTimeOffset now)
    {
        Items.Children.Clear();
        foreach (IslandNote note in _shown)
        {
            Items.Children.Add(CreateRow(note, now));
        }
    }

    private Grid CreateRow(IslandNote note, DateTimeOffset now)
    {
        var row = new Grid { Height = RowHeight, ColumnSpacing = 8, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var copy = new Button
        {
            Content = new TextBlock { Text = note.Text, FontSize = 13, MaxLines = 1, TextTrimming = TextTrimming.CharacterEllipsis },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Style = (Style)Resources["IslandChipButtonStyle"],
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            FontWeight = Microsoft.UI.Text.FontWeights.Normal,
            Padding = new Thickness(8, 0, 8, 0),
        };
        ToolTipService.SetToolTip(copy, Strings.Get("Island_CopyNote"));
        copy.Click += (_, _) =>
        {
            var package = new DataPackage();
            package.SetText(note.Text);
            Clipboard.SetContent(package);
        };
        PressFeedback.Attach(copy);
        row.Children.Add(copy);

        var when = new TextBlock
        {
            Text = Age(note.Created, now),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(IslandPalette.TextTertiary),
        };
        Grid.SetColumn(when, 1);
        row.Children.Add(when);

        var remove = new Button
        {
            Content = new FontIcon { Glyph = "\xE711", FontSize = 11 },
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(14),
            Style = (Style)Resources["IslandIconButtonStyle"],
        };
        string removeName = Strings.Get("Island_RemoveNote");
        AutomationProperties.SetName(remove, removeName);
        ToolTipService.SetToolTip(remove, removeName);
        remove.Click += (_, _) => Commands?.RemoveNote(note.Text);
        Grid.SetColumn(remove, 2);
        row.Children.Add(remove);
        return row;
    }

    /// <summary>"now", "5 min", "3 h" or the date, for how long ago a note was written.</summary>
    private static string Age(DateTimeOffset created, DateTimeOffset now)
    {
        TimeSpan age = now - created;
        return age.TotalMinutes < 1 ? Strings.Get("Island_AgeNow")
            : age.TotalHours < 1 ? Strings.Format("Island_MinutesFormat", (int)age.TotalMinutes)
            : age.TotalDays < 1 ? Strings.Format("Island_AgeHoursFormat", (int)age.TotalHours)
            : created.ToLocalTime().ToString("d MMM", System.Globalization.CultureInfo.CurrentCulture);
    }
}
