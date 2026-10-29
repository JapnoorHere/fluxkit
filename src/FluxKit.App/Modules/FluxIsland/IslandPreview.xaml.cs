using FluxKit.App.Common;
using FluxKit.Tools.FluxIsland;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Xaml.Controls;
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

    public IslandPreview()
    {
        InitializeComponent();
        Samples.SelectedItem = Samples.Items[0];
        Loaded += (_, _) =>
        {
            ScreenWallpaper.Source = _wallpapers.Load(WallpaperCache.CurrentWallpaper(), 480);
            Pill.SetActive(true);
            Show();
        };
        Unloaded += (_, _) => Pill.SetActive(false);
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
