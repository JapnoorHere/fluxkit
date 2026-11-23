using FluxKit.App.Common;
using FluxKit.Tools.FluxIsland;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI.ViewManagement;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// The settings page preview: a real island showing one of the <see cref="IslandSamples"/>, following the settings
/// as they change. Commands are never set, so its buttons can be tried without touching music or timers.
/// </summary>
public sealed partial class IslandPreview : UserControl
{
    private readonly WallpaperCache _wallpapers = new();
    private readonly UISettings _uiSettings = new();
    private FluxIslandSettings _settings = new();
    private string _sample = "music";

    /// <summary>The gestures card: an icon and a line for each thing the island does that nothing on it hints at.</summary>
    private static readonly (string Glyph, string Key)[] GestureLines =
    [
        ("", "IslandPreview_GestureOpen"),
        ("", "IslandPreview_GestureVolume"),
        ("", "IslandPreview_GestureDismiss"),
        ("", "IslandPreview_GestureFlick"),
        ("", "IslandPreview_GestureTearOff"),
        ("", "IslandPreview_GestureScrub"),
        ("", "IslandPreview_GestureDrop"),
        ("", "IslandPreview_GestureShortcut"),
    ];

    public IslandPreview()
    {
        InitializeComponent();
        BuildGestures();
        Samples.SelectedItem = Samples.Items[0];
        Loaded += (_, _) =>
        {
            ScreenWallpaper.Source = _wallpapers.Load(WallpaperCache.CurrentWallpaper(), 480);
            Pill.SetActive(true);
            Show();
        };
        Unloaded += (_, _) => Pill.SetActive(false);
    }

    private void BuildGestures()
    {
        foreach ((string glyph, string key) in GestureLines)
        {
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            var icon = new FontIcon { Glyph = glyph, FontSize = 14, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) };
            icon.Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"];
            var text = new TextBlock { Text = Strings.Get(key), TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(text, 1);
            row.Children.Add(icon);
            row.Children.Add(text);
            Gestures.Children.Add(row);
        }
    }

    public void Update(FluxIslandSettings settings)
    {
        _settings = settings;
        Show();
    }

    private void OnSampleChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem?.Tag is string sample && sample != _sample)
        {
            _sample = sample;
            Show();
        }
    }

    private void Show()
    {
        if (!IsLoaded)
        {
            return;
        }

        IslandScene scene = IslandSamples.Create(_sample, DateTimeOffset.Now) with { Settings = _settings };
        Pill.Springs = _settings.Effects == IslandEffects.Full && _uiSettings.AnimationsEnabled;
        Pill.Preview(scene, open: false);
    }
}
