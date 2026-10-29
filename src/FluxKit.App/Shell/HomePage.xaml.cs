using FluxKit.Core.Tools;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FluxKit.App.Shell;

/// <summary>Overview: one card per registered tool plus anything that needs the user's attention.</summary>
public sealed partial class HomePage : Page
{
    private readonly AppHost _host = AppHost.Current;

    public HomePage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _host.StatusChanged += OnStatusChanged;
            Refresh();
        };
        Unloaded += (_, _) => _host.StatusChanged -= OnStatusChanged;
    }

    private void OnStatusChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        ToolCards.ItemsSource = _host.Registry.All.Select(BuildCard).ToList();
        UpdateAttention();
    }

    private void UpdateAttention()
    {
        if (_host.Settings.RecoveredFromCorruption)
        {
            Show(InfoBarSeverity.Warning, "Your settings were reset", "The settings file was damaged, so FluxKit started with the default settings. A copy of the damaged file is kept in the FluxKit folder.");
            return;
        }

        IFluxTool? troubled = _host.Registry.All.FirstOrDefault(u => u.State.Status is ToolStatus.Degraded or ToolStatus.Faulted);
        if (troubled is not null)
        {
            Show(
                troubled.State.Status == ToolStatus.Faulted ? InfoBarSeverity.Error : InfoBarSeverity.Warning,
                troubled.State.Status == ToolStatus.Faulted ? $"{troubled.Descriptor.DisplayName} has stopped" : $"{troubled.Descriptor.DisplayName} is running in limited mode",
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

    private UIElement BuildCard(IFluxTool tool)
    {
        ToolDescriptor d = tool.Descriptor;
        bool enabled = _host.Registry.IsEnabled(d.Id);
        var toggle = new ToggleSwitch { IsOn = enabled, OnContent = "On", OffContent = "Off", MinWidth = 0 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(toggle, $"Turn on {d.DisplayName}");
        toggle.Toggled += async (_, _) => await _host.SetToolEnabledAsync(d.Id, toggle.IsOn);

        var header = new Grid { ColumnSpacing = 12 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(d.IconAsset is { } asset
            ? new Image { Width = 32, Height = 32, Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri($"ms-appx:///Assets/{asset}")) }
            : new FontIcon { Glyph = d.Glyph, FontSize = 24, Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"] });
        var title = new TextBlock { Text = d.DisplayName, Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"], FontSize = 18, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(title, 1);
        Grid.SetColumn(toggle, 2);
        header.Children.Add(title);
        header.Children.Add(toggle);

        string status = tool.State.Status switch
        {
            ToolStatus.Running => "On and ready",
            ToolStatus.Degraded => "Limited mode. " + (tool.State.Detail ?? string.Empty),
            ToolStatus.Faulted => "Stopped because of an error",
            ToolStatus.Starting => "Starting…",
            _ => "Off",
        };

        var open = new Button { Content = "Settings", HorizontalAlignment = HorizontalAlignment.Right };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(open, $"{d.DisplayName} settings");
        open.Click += (_, _) => _host.ShowMainWindow(d.Id);

        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(header);
        body.Children.Add(new TextBlock { Text = d.Description, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
        body.Children.Add(new TextBlock { Text = "Status: " + status, Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"], TextWrapping = TextWrapping.Wrap });
        body.Children.Add(open);

        return new Border
        {
            Padding = new Thickness(20),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)Application.Current.Resources["OverlayCornerRadius"],
            Child = body,
        };
    }
}
