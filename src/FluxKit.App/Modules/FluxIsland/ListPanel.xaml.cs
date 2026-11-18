using System.Numerics;
using FluxKit.App.Modules.FluxIsland.Design;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>A small button on a list row, such as remove or pin. <see cref="Removes"/> plays the row's exit first.</summary>
internal sealed record ListAction(string Glyph, string Name, Action Run, bool Active = false, bool Removes = false);

/// <summary>
/// One row of a list page. <see cref="Key"/> identifies it; <see cref="Open"/> runs when the row is clicked. A null
/// <see cref="Glyph"/> uses the page's own badge.
/// </summary>
internal sealed record ListItem(string Key, string Text, string? Caption, string? Glyph, Color Accent, Action? Open, string? OpenName, IReadOnlyList<ListAction> Actions);

/// <summary>What a list page shows and how: set once by the pill with <see cref="ListPanel.Configure"/>.</summary>
internal sealed record ListPageSpec(
    string Glyph,
    Color Accent,
    string TitleKey,
    string NameFormatKey,
    string EmptyKey,
    Func<IslandScene, DateTimeOffset, IReadOnlyList<ListItem>> Items,
    Action Clear,
    Action Undo);

/// <summary>
/// The list page used for notes, recent moments (Rewind) and clipboard history. Rows are rebuilt only when the list
/// actually changes. Clear turns into Undo for a few seconds instead of asking "are you sure?".
/// </summary>
public sealed partial class ListPanel : UserControl, IIslandPage
{
    private const double RowHeight = 44;
    private const int VisibleRows = 3;
    private static readonly TimeSpan UndoTime = TimeSpan.FromSeconds(4);

    private readonly DispatcherQueueTimer _undoTimer;
    private ListPageSpec? _spec;
    private string _signature = "\0";
    private int _count;
    private bool _animate = true;

    public ListPanel()
    {
        InitializeComponent();
        Count.Foreground = new SolidColorBrush(IslandPalette.TextTertiary);
        EmptyText.Foreground = new SolidColorBrush(IslandPalette.TextSecondary);
        _undoTimer = DispatcherQueue.CreateTimer();
        _undoTimer.Interval = UndoTime;
        _undoTimer.IsRepeating = false;
        _undoTimer.Tick += (_, _) => ShowUndo(false);
        ClearButton.Click += (_, _) =>
        {
            _spec?.Clear();
            ShowUndo(true);
        };
        UndoButton.Click += (_, _) =>
        {
            _spec?.Undo();
            ShowUndo(false);
        };
        PressFeedback.Attach(ClearButton);
        PressFeedback.Attach(UndoButton);
    }

    UIElement[] IIslandPage.Rows => [Header, _count == 0 ? EmptyState : ItemsHost];

    Size IIslandPage.PreferredSize => new(420, 58 + (RowHeight * Math.Clamp(_count, 1, VisibleRows)));

    string IIslandPage.State => _count == 0 ? "empty" : "list";

    string IIslandPage.Update(IslandScene scene, DateTimeOffset now, bool springs)
    {
        if (_spec is null)
        {
            return string.Empty;
        }

        _animate = springs;
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
        Count.Visibility = _count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = _count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ItemsHost.Visibility = _count == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (UndoButton.Visibility != Visibility.Visible)
        {
            ClearButton.Visibility = _count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        return Strings.Format(_spec.NameFormatKey, _count);
    }

    internal void Configure(ListPageSpec spec)
    {
        _spec = spec;
        HeaderGlyph.Show(spec.Glyph, spec.Accent);
        EmptyBadge.Show(spec.Glyph, spec.Accent);
        Title.Text = Strings.Get(spec.TitleKey);
        EmptyText.Text = Strings.Get(spec.EmptyKey);
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

    private void ShowUndo(bool on)
    {
        UndoButton.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        ClearButton.Visibility = !on && _count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _undoTimer.Stop();
        if (on)
        {
            _undoTimer.Start();
            IslandMotion.Pop(UndoButton, _animate);
        }
    }

    /// <summary>
    /// A row: one wide button (badge, text, time) that opens the item, and the item's own small actions on the right,
    /// shown only while the row is hovered so the list stays calm.
    /// </summary>
    private Grid CreateRow(ListItem item)
    {
        var row = new Grid { Height = RowHeight, ColumnSpacing = 4 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var badge = new IconBadge { BadgeSize = 28 };
        badge.Show(item.Glyph ?? _spec!.Glyph, item.Glyph is null ? _spec!.Accent : item.Accent);
        var text = new TextBlock
        {
            Text = item.Text,
            FontSize = 13,
            MaxLines = 1,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var caption = new TextBlock
        {
            Text = item.Caption ?? string.Empty,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(IslandPalette.TextTertiary),
        };
        var content = new Grid { ColumnSpacing = 12 };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.Children.Add(badge);
        Grid.SetColumn(text, 1);
        content.Children.Add(text);
        Grid.SetColumn(caption, 2);
        content.Children.Add(caption);

        var open = new Button
        {
            Content = content,
            Height = RowHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Style = (Style)Resources["IslandTileButtonStyle"],
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(8, 0, 10, 0),
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

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        foreach (ListAction action in item.Actions)
        {
            var glyph = new FontIcon { Glyph = action.Glyph, FontSize = 12, Foreground = new SolidColorBrush(action.Active ? IslandPalette.Yellow : IslandPalette.TextSecondary) };
            var button = new Button { Content = glyph, Width = 30, Height = 30, CornerRadius = new CornerRadius(15), Style = (Style)Resources["IslandIconButtonStyle"] };
            AutomationProperties.SetName(button, action.Name);
            ToolTipService.SetToolTip(button, action.Name);
            button.Click += (_, _) =>
            {
                if (action.Removes)
                {
                    Leave(row, action.Run);
                }
                else
                {
                    IslandMotion.Pop(glyph, _animate);
                    action.Run();
                }
            };
            actions.Children.Add(button);
        }

        // Pinned items keep their pin visible; other actions wait for the pointer (or keyboard focus).
        bool keepVisible = item.Actions.Any(a => a.Active);
        actions.Opacity = keepVisible ? 1 : 0;
        row.PointerEntered += (_, _) => Fade(actions, 1);
        row.PointerExited += (_, _) => Fade(actions, keepVisible ? 1 : 0);
        open.GotFocus += (_, _) => Fade(actions, 1);
        Grid.SetColumn(actions, 1);
        row.Children.Add(actions);
        return row;
    }

    /// <summary>A removed row shrinks and fades away first (plan I10), then the item really goes.</summary>
    private void Leave(Grid row, Action remove)
    {
        if (!_animate)
        {
            remove();
            return;
        }

        Visual visual = ElementCompositionPreview.GetElementVisual(row);
        visual.CenterPoint = new Vector3(row.ActualSize.X / 2, row.ActualSize.Y / 2, 0);
        Compositor compositor = visual.Compositor;
        CompositionScopedBatch batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        ScalarKeyFrameAnimation fade = compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(1, 0, IslandSprings.EaseIn(compositor));
        fade.Duration = TimeSpan.FromMilliseconds(180);
        visual.StartAnimation("Opacity", fade);
        Vector3KeyFrameAnimation shrink = compositor.CreateVector3KeyFrameAnimation();
        shrink.InsertKeyFrame(1, new Vector3(0.94f, 0.6f, 1), IslandSprings.EaseIn(compositor));
        shrink.Duration = fade.Duration;
        visual.StartAnimation("Scale", shrink);
        batch.End();
        batch.Completed += (_, _) => remove();
    }

    /// <summary>Row actions fade in under the pointer and out again, on the compositor.</summary>
    private void Fade(UIElement element, float to)
    {
        if (!_animate)
        {
            element.Opacity = to;
            return;
        }

        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        float from = (float)element.Opacity;
        element.Opacity = 1;
        ScalarKeyFrameAnimation fade = visual.Compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0, from);
        fade.InsertKeyFrame(1, to);
        fade.Duration = TimeSpan.FromMilliseconds(140);
        visual.StartAnimation("Opacity", fade);
    }
}
