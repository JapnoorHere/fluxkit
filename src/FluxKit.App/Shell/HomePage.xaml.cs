using FluxKit.App.Controls;
using FluxKit.Core.Tools;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FluxKit.App.Shell;

/// <summary>The launchpad: a tile per registered tool, plus anything that needs the user's attention.</summary>
public sealed partial class HomePage : Page
{
    private readonly AppHost _host = AppHost.Current;
    private readonly Dictionary<string, ToolTile> _tiles = [];

    public HomePage()
    {
        InitializeComponent();
        GreetingText.Text = Strings.Get(DateTime.Now.Hour switch
        {
            < 12 => "Home_GreetingMorning",
            < 18 => "Home_GreetingAfternoon",
            _ => "Home_GreetingEvening",
        });
        BuildTiles();
        Loaded += (_, _) =>
        {
            _host.StatusChanged += OnStatusChanged;
            Refresh();
        };
        Unloaded += (_, _) => _host.StatusChanged -= OnStatusChanged;
    }

    private void OnStatusChanged(object? sender, EventArgs e) => Refresh();

    private void BuildTiles()
    {
        var tiles = new List<UIElement>();
        foreach (IFluxTool tool in _host.Registry.All)
        {
            ToolDescriptor d = tool.Descriptor;
            var tile = new ToolTile { Title = d.DisplayName, Description = d.Description };
            tile.SetIcon(d.Glyph, d.IconAsset);
            tile.SetAccessibleNames(Strings.Format("Home_OpenToolFormat", d.DisplayName), Strings.Format("Home_ToggleToolFormat", d.DisplayName));
            tile.Opened += (_, _) => _host.ShowMainWindow(d.Id);
            tile.EnabledChanged += async (_, on) =>
            {
                try
                {
                    await _host.SetToolEnabledAsync(d.Id, on);
                }
                catch (Exception ex)
                {
                    _host.Log.Error(ex, "Turning {Tool} on or off failed", d.Id);
                }
            };
            _tiles[d.Id] = tile;
            tiles.Add(tile);
        }

        var more = new ToolTile { Title = Strings.Get("Home_MoreTitle"), Description = Strings.Get("Home_MoreText") };
        more.MakePlaceholder();
        tiles.Add(more);
        ToolTiles.ItemsSource = tiles;
    }

    private void Refresh()
    {
        int on = 0;
        bool limited = false;
        foreach (IFluxTool tool in _host.Registry.All)
        {
            ToolTile tile = _tiles[tool.Descriptor.Id];
            tile.IsOn = _host.Registry.IsEnabled(tool.Descriptor.Id);
            tile.StatusText = StatusText(tool.State);
            tile.SetStatusTone(StatusTones.For(tool.State.Status));
            on += tool.State.IsActive ? 1 : 0;
            limited |= tool.State.Status is ToolStatus.Degraded or ToolStatus.Faulted;
        }

        ChipText.Text = Strings.Format("Home_ToolsOnFormat", on, _tiles.Count);
        ChipDot.Fill = StatusTones.Brush(limited ? StatusTone.Caution : on > 0 ? StatusTone.Good : StatusTone.Neutral);
        UpdateAttention();
    }

    private static string StatusText(ToolState state) => state.Status switch
    {
        ToolStatus.Running => Strings.Get("Status_Running"),
        ToolStatus.Degraded => Strings.Format("Status_LimitedFormat", state.Detail ?? string.Empty),
        ToolStatus.Faulted => Strings.Get("Status_Faulted"),
        ToolStatus.Starting => Strings.Get("Status_Starting"),
        _ => Strings.Get("Status_Off"),
    };

    private void UpdateAttention()
    {
        if (_host.Settings.RecoveredFromCorruption)
        {
            Show(InfoBarSeverity.Warning, Strings.Get("Home_SettingsResetTitle"), Strings.Get("Home_SettingsResetText"));
            return;
        }

        IFluxTool? troubled = _host.Registry.All.FirstOrDefault(u => u.State.Status is ToolStatus.Degraded or ToolStatus.Faulted);
        if (troubled is not null)
        {
            bool faulted = troubled.State.Status == ToolStatus.Faulted;
            Show(
                faulted ? InfoBarSeverity.Error : InfoBarSeverity.Warning,
                Strings.Format(faulted ? "Home_ToolStoppedFormat" : "Home_ToolLimitedFormat", troubled.Descriptor.DisplayName),
                troubled.State.Detail ?? string.Empty);
            return;
        }

        AttentionBar.IsOpen = false;
    }

    private void Show(InfoBarSeverity severity, string title, string message)
    {
        AttentionBar.Severity = severity;
        AttentionBar.Title = title;
        AttentionBar.Message = message;
        AttentionBar.IsOpen = true;
    }
}
