using System.Numerics;
using FluxKit.App.Modules.FluxIsland.Design;
using FluxKit.Platform.Shell;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>What a drop zone does with the files dropped on it.</summary>
public enum DropAction
{
    Shelf,
    CopyPath,
    Zip,
    Convert,
    Resize,
}

/// <summary>
/// The drop zones shown while files are dragged over the island. The pill tells it where the pointer is
/// (<see cref="ZoneAt"/>, <see cref="Highlight"/>); the zone under it lifts and lights up in its colour.
/// Image-only zones are dimmed unless every dragged file is an image.
/// </summary>
public sealed partial class DropZones : UserControl
{
    private static readonly (DropAction Action, string Glyph, string Key, Color Accent)[] Definitions =
    [
        (DropAction.Shelf, "\xE7B8", "Island_DropShelf", IslandPalette.Violet),
        (DropAction.CopyPath, "\xE8C8", "Island_DropCopyPath", IslandPalette.Cyan),
        (DropAction.Zip, "\xF012", "Island_DropZip", IslandPalette.Yellow),
        (DropAction.Convert, "\xE8AB", "Island_DropConvert", IslandPalette.Green),
        (DropAction.Resize, "\xE740", "Island_DropResize", IslandPalette.Orange),
    ];

    private readonly Dictionary<DropAction, (Border Tile, TextBlock Label, FontIcon Icon, Color Accent)> _tiles = [];
    private bool _images;
    private DropAction? _lit;

    public DropZones()
    {
        InitializeComponent();
        Heading.Foreground = new SolidColorBrush(IslandPalette.TextSecondary);
        for (int i = 0; i < Definitions.Length; i++)
        {
            (DropAction action, string glyph, string key, Color accent) = Definitions[i];
            Zones.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var icon = new FontIcon { Glyph = glyph, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center };
            var label = new TextBlock { Text = Strings.Get(key), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            var tile = new Border
            {
                CornerRadius = new CornerRadius(IslandTokens.TileRadius),
                BorderThickness = new Thickness(1.5),
                Child = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 6, Children = { icon, label } },
            };
            Grid.SetColumn(tile, i);
            Zones.Children.Add(tile);
            _tiles[action] = (tile, label, icon, accent);
        }

        Paint();
    }

    internal static Size PreferredSize => new(420, 128);

    /// <summary>Tells the zones what is being dragged, so image zones can light up only for images.</summary>
    public void SetFiles(IReadOnlyList<string> paths)
    {
        _images = paths.Count > 0 && paths.All(FileTransforms.IsImage);
        Paint();
    }

    /// <summary>The usable zone at <paramref name="point"/> (relative to this control), or null between zones.</summary>
    public DropAction? ZoneAt(Point point)
    {
        foreach ((DropAction action, (Border tile, _, _, _)) in _tiles)
        {
            Rect bounds = tile.TransformToVisual(this).TransformBounds(new Rect(0, 0, tile.ActualWidth, tile.ActualHeight));
            if (bounds.Contains(point))
            {
                return IsUsable(action) ? action : null;
            }
        }

        return null;
    }

    public void Highlight(DropAction? action, bool springs)
    {
        if (action == _lit)
        {
            return;
        }

        _lit = action;
        Paint();
        foreach ((DropAction zone, (Border tile, _, _, _)) in _tiles)
        {
            Visual visual = ElementCompositionPreview.GetElementVisual(tile);
            visual.CenterPoint = new Vector3(tile.ActualSize / 2, 0);
            float scale = zone == action ? 1.06f : 1f;
            if (springs)
            {
                visual.StartAnimation("Scale", IslandSprings.Vector3(visual.Compositor, IslandSprings.Bouncy, new Vector3(scale, scale, 1)));
            }
            else
            {
                visual.Scale = new Vector3(scale, scale, 1);
            }
        }
    }

    /// <summary>What the drag caption should say for <paramref name="action"/>.</summary>
    public static string Caption(DropAction action) => Strings.Get(Definitions.First(d => d.Action == action).Key);

    private bool IsUsable(DropAction action) => action is not (DropAction.Convert or DropAction.Resize) || _images;

    private void Paint()
    {
        foreach ((DropAction action, (Border tile, TextBlock label, FontIcon icon, Color accent)) in _tiles)
        {
            bool usable = IsUsable(action);
            bool lit = action == _lit;
            tile.Opacity = usable ? 1 : 0.35;
            tile.Background = new SolidColorBrush(lit ? IslandPalette.WithAlpha(accent, 0x2E) : IslandPalette.Chip);
            tile.BorderBrush = new SolidColorBrush(lit ? accent : Microsoft.UI.Colors.Transparent);
            icon.Foreground = new SolidColorBrush(lit || usable ? accent : IslandPalette.TextSecondary);
            label.Foreground = new SolidColorBrush(lit ? IslandPalette.TextPrimary : IslandPalette.TextSecondary);
        }
    }
}
