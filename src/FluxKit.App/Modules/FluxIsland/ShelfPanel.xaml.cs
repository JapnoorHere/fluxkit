using FluxKit.App.Modules.FluxIsland.Design;
using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// The shelf page. Tiles are kept between updates, so each file's icon is looked up once and only new files pop in.
/// </summary>
public sealed partial class ShelfPanel : UserControl, IIslandPage
{
    private static readonly Size PageSize = new(420, 148);

    private readonly Dictionary<string, ShelfTile> _tiles = new(StringComparer.OrdinalIgnoreCase);
    private bool _dropping;
    private bool _breathing;

    public ShelfPanel()
    {
        InitializeComponent();
        Count.Foreground = new SolidColorBrush(IslandPalette.TextSecondary);
        HeaderGlyph.Show("\xE7B8", IslandPalette.Violet);
        ClearButton.Click += (_, _) => Commands?.ClearShelf();
        ShareButton.Click += (_, _) => ShareRequested?.Invoke(this, [.. _tiles.Keys.Where(File.Exists)]);
        PressFeedback.Attach(ShareButton);
        PressFeedback.Attach(ClearButton);

        // A mouse wheel only scrolls up and down; turn it sideways so a long shelf can be scrolled.
        Scroller.PointerWheelChanged += OnWheel;
    }

    public IIslandCommands? Commands { get; set; }

    /// <summary>False when effects are reduced: tiles and the drop zone appear without motion.</summary>
    public bool Animate { get; set; } = true;

    /// <summary>True while one of the shelf's own files is being dragged out.</summary>
    public bool IsDraggingOut { get; private set; }

    /// <summary>Files to hand to the Windows share sheet.</summary>
    public event EventHandler<IReadOnlyList<string>>? ShareRequested;

    /// <summary>True while a tile's right-click menu is open.</summary>
    public event EventHandler<bool>? MenuShown;

    UIElement[] IIslandPage.Rows => [Header, ItemsHost];

    Size IIslandPage.PreferredSize => PageSize;

    string IIslandPage.State => "shelf";

    string IIslandPage.Update(IslandScene scene, DateTimeOffset now, bool springs)
    {
        Animate = springs;
        Update(scene.Shelf);
        return Strings.Format("Island_Shelf_CountFormat", scene.Shelf.Count);
    }

    public void Update(IReadOnlyList<string> paths)
    {
        Count.Text = paths.Count > 0 ? paths.Count.ToString(System.Globalization.CultureInfo.CurrentCulture) : string.Empty;
        ClearButton.Visibility = paths.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ShareButton.Visibility = paths.Any(File.Exists) ? Visibility.Visible : Visibility.Collapsed;

        foreach (string gone in _tiles.Keys.Where(k => !paths.Contains(k, StringComparer.OrdinalIgnoreCase)).ToList())
        {
            Items.Children.Remove(_tiles[gone]);
            _tiles.Remove(gone);
        }

        for (int i = 0; i < paths.Count; i++)
        {
            bool added = false;
            if (!_tiles.TryGetValue(paths[i], out ShelfTile? tile))
            {
                tile = CreateTile(paths[i]);
                _tiles[paths[i]] = tile;
                added = true;
            }

            // Newest first: put each tile at its place, moving only the ones that changed position.
            int at = Items.Children.IndexOf(tile);
            if (at != i)
            {
                if (at >= 0)
                {
                    Items.Children.RemoveAt(at);
                }

                Items.Children.Insert(i, tile);
            }

            tile.Animate = Animate;
            if (added)
            {
                IslandMotion.Enter(tile, IslandMotion.Stagger * i, Animate);
                IslandMotion.Pop(tile, Animate);
            }
        }

        UpdateDropZone();
    }

    /// <summary>Shows or hides the glowing drop zone for a drag coming in from outside.</summary>
    public void SetDropping(bool dropping)
    {
        if (dropping == _dropping)
        {
            return;
        }

        _dropping = dropping;
        UpdateDropZone();
    }

    private void UpdateDropZone()
    {
        bool empty = _tiles.Count == 0;
        DropText.Text = Strings.Get(empty ? "Island_ShelfDropHere" : "Island_ShelfDropToAdd");
        Visual zone = ElementCompositionPreview.GetElementVisual(DropZone);
        Scroller.Opacity = _dropping && !empty ? 0.25 : 1;

        // An empty shelf always shows where to drop, calmly; during a drag the zone lights up and breathes.
        Color accent = _dropping ? IslandPalette.Cyan : IslandPalette.TextSecondary;
        DropOutline.Stroke = new SolidColorBrush(Color.FromArgb(_dropping ? (byte)0xFF : (byte)0x66, accent.R, accent.G, accent.B));
        DropOutline.Fill = new SolidColorBrush(Color.FromArgb(_dropping ? (byte)0x1A : (byte)0x00, accent.R, accent.G, accent.B));
        DropGlyph.Foreground = new SolidColorBrush(accent);
        DropText.Foreground = new SolidColorBrush(_dropping ? IslandPalette.TextPrimary : IslandPalette.TextSecondary);
        DropZone.Visibility = _dropping || empty ? Visibility.Visible : Visibility.Collapsed;
        if (!_dropping)
        {
            zone.StopAnimation("Opacity");
            zone.Opacity = 1;
            _breathing = false;
            return;
        }

        if (_breathing || !Animate)
        {
            return;
        }

        _breathing = true;

        // The zone breathes gently while a file hovers over it; it runs only during a drag.
        ScalarKeyFrameAnimation breathe = zone.Compositor.CreateScalarKeyFrameAnimation();
        breathe.InsertKeyFrame(0, 0.65f);
        breathe.InsertKeyFrame(0.5f, 1);
        breathe.InsertKeyFrame(1, 0.65f);
        breathe.Duration = TimeSpan.FromMilliseconds(1400);
        breathe.IterationBehavior = AnimationIterationBehavior.Forever;
        zone.StartAnimation("Opacity", breathe);
    }

    private ShelfTile CreateTile(string path)
    {
        var tile = new ShelfTile(path);
        tile.RemoveRequested += (_, _) => Commands?.RemoveFromShelf(tile.FilePath);
        tile.DragBegan += (_, _) => IsDraggingOut = true;
        tile.DragFinished += (_, _) => IsDraggingOut = false;
        tile.ShareRequested += (_, _) => ShareRequested?.Invoke(this, [tile.FilePath]);
        tile.MenuShown += (_, open) => MenuShown?.Invoke(this, open);
        return tile;
    }

    private void OnWheel(object sender, PointerRoutedEventArgs e)
    {
        int delta = e.GetCurrentPoint(Scroller).Properties.MouseWheelDelta;
        if (Scroller.ScrollableWidth <= 0 || delta == 0)
        {
            return;
        }

        Scroller.ChangeView(Scroller.HorizontalOffset - delta, null, null, disableAnimation: !Animate);
        e.Handled = true;
    }
}
