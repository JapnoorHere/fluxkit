using FluxKit.Platform.Shell;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// One file on the shelf. The file is looked up once, for its icon and so it can be dragged out; dragging out offers
/// copy or link only, so the file always stays where it is.
/// </summary>
public sealed partial class ShelfTile : UserControl
{
    private const string FolderGlyph = "\xE8B7";
    private static readonly Color RestFill = Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF);
    private static readonly Color HoverFill = Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF);

    private IStorageItem? _item;

    public ShelfTile(string path)
    {
        InitializeComponent();
        FilePath = path;
        bool folder = Directory.Exists(path);
        bool exists = folder || File.Exists(path);
        string name = System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(path));
        FileName.Text = string.IsNullOrEmpty(name) ? path : name;
        FileName.Foreground = new SolidColorBrush(IslandPalette.TextPrimary);
        Fallback.Foreground = new SolidColorBrush(IslandPalette.TextSecondary);
        if (folder)
        {
            Fallback.Glyph = FolderGlyph;
        }

        AutomationProperties.SetName(this, FileName.Text);
        ToolTipService.SetToolTip(this, FileName.Text);

        // A file that was moved or deleted since stays visible, faded, until it is removed; it can't be dragged out.
        Opacity = exists ? 1 : 0.45;
        CanDrag = exists;

        RemoveButton.Click += (_, _) => RemoveRequested?.Invoke(this, EventArgs.Empty);
        PressFeedback.Attach(RemoveButton);
        PressFeedback.Attach(this);
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        DragStarting += OnDragStarting;
        DropCompleted += (_, _) => DragFinished?.Invoke(this, EventArgs.Empty);
        DoubleTapped += (_, _) => Open();
        ContextFlyout = BuildMenu(exists, folder);
        if (exists)
        {
            _ = LoadAsync(folder);
        }
    }

    public string FilePath { get; }

    /// <summary>False when effects are reduced: the remove button appears without a pop.</summary>
    public bool Animate { get; set; } = true;

    public event EventHandler? RemoveRequested;

    /// <summary>Raised when this tile starts being dragged out, so the island can ignore its own drag.</summary>
    public event EventHandler? DragBegan;

    public event EventHandler? DragFinished;

    public event EventHandler? ShareRequested;

    /// <summary>Raised with true when the right-click menu opens and false when it closes, so the island stays open meanwhile.</summary>
    public event EventHandler<bool>? MenuShown;

    /// <summary>Open, Show in folder, Share, Copy path and Remove. A missing file only offers Show in folder and Remove.</summary>
    private MenuFlyout BuildMenu(bool exists, bool folder)
    {
        var menu = new MenuFlyout();
        if (exists)
        {
            menu.Items.Add(Item("Island_Shelf_Open", "\xE8E5", Open));
        }

        menu.Items.Add(Item("Island_Action_ShowInFolder", "\xE838", () => ShellLauncher.ShowInFolder(FilePath)));
        if (exists && !folder)
        {
            menu.Items.Add(Item("Island_Shelf_Share", "\xE72D", () => ShareRequested?.Invoke(this, EventArgs.Empty)));
        }

        menu.Items.Add(Item("Island_Shelf_CopyPath", "\xE8C8", CopyPath));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Item("Island_Shelf_Remove", "\xE711", () => RemoveRequested?.Invoke(this, EventArgs.Empty)));
        menu.Opened += (_, _) => MenuShown?.Invoke(this, true);
        menu.Closed += (_, _) => MenuShown?.Invoke(this, false);
        return menu;
    }

    private static MenuFlyoutItem Item(string key, string glyph, Action action)
    {
        var item = new MenuFlyoutItem { Text = Strings.Get(key), Icon = new FontIcon { Glyph = glyph } };
        item.Click += (_, _) => action();
        return item;
    }

    /// <summary>Opens the file or folder the way a double-click in File Explorer would.</summary>
    private void Open() => ShellLauncher.Open(FilePath);

    private void CopyPath()
    {
        var package = new DataPackage();
        package.SetText(FilePath);
        Clipboard.SetContent(package);
    }

    private async Task LoadAsync(bool folder)
    {
        try
        {
            _item = folder ? await StorageFolder.GetFolderFromPathAsync(FilePath) : await StorageFile.GetFileFromPathAsync(FilePath);
            if (_item is IStorageItemProperties properties)
            {
                // SingleItem gives a picture's own preview and every other file its icon.
                using StorageItemThumbnail thumbnail = await properties.GetThumbnailAsync(ThumbnailMode.SingleItem, 48, ThumbnailOptions.UseCurrentScale);
                if (thumbnail is not null)
                {
                    var image = new BitmapImage();
                    await image.SetSourceAsync(thumbnail);
                    Icon.Source = image;
                    Fallback.Visibility = Visibility.Collapsed;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            // No icon and no drag-out; the name still shows. The path is never logged.
            CanDrag = _item is not null;
        }
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        Card.Background = new SolidColorBrush(HoverFill);
        RemoveButton.Visibility = Visibility.Visible;
        IslandMotion.Pop(RemoveButton, Animate);
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        Card.Background = new SolidColorBrush(RestFill);
        RemoveButton.Visibility = Visibility.Collapsed;
    }

    private void OnDragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (_item is null)
        {
            args.Cancel = true;
            return;
        }

        args.Data.SetStorageItems([_item], readOnly: true);
        args.Data.RequestedOperation = DataPackageOperation.Copy;
        args.AllowedOperations = DataPackageOperation.Copy | DataPackageOperation.Link;
        if (Icon.Source is BitmapImage icon)
        {
            args.DragUI.SetContentFromBitmapImage(icon);
        }

        DragBegan?.Invoke(this, EventArgs.Empty);
    }
}
