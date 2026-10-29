using FluxKit.App.Modules.DragToSwitch;
using FluxKit.Core.Settings;
using FluxKit.Core.Tools;
using FluxKit.Platform.Windows;
using FluxKit.Tools.DragToSwitch;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Graphics;

namespace FluxKit.App.Shell;

/// <summary>The settings window: Mica, custom title bar, registry-driven navigation.</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Settings page for each tool id. Adding a tool adds one entry here.</summary>
    private static readonly Dictionary<string, Type> ToolPages = new()
    {
        [DragToSwitchTool.Id] = typeof(DragToSwitchPage),
    };

    private readonly AppHost _host = AppHost.Current;
    private readonly Dictionary<string, NavigationViewItem> _toolItems = [];
    private bool _firstActivation = true;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SystemBackdrop = new MicaBackdrop();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "FluxKit.ico"));
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        PreviewBadge.Visibility = _host.Version.Contains('-', StringComparison.Ordinal) ? Visibility.Visible : Visibility.Collapsed;

        RestorePlacement();
        BuildNavigation();
        ApplyTheme();

        _host.ThemeChanged += OnThemeChanged;
        _host.StatusChanged += OnStatusChanged;
        Activated += OnActivated;
        Closed += OnClosed;
        RootGrid.ActualThemeChanged += (_, _) => UpdateCaptionButtons();
        Nav.SelectedItem = Nav.MenuItems[0];
    }

    public void NavigateTo(string tag)
    {
        object? item = Nav.MenuItems.Concat(Nav.FooterMenuItems).OfType<NavigationViewItem>().FirstOrDefault(i => (string)i.Tag == tag);
        if (item is not null)
        {
            Nav.SelectedItem = item;
        }
    }

    public void BringToFront()
    {
        nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
        {
            presenter.Restore();
        }

        WindowHelpers.BringToForeground(hwnd);
    }

    private void BuildNavigation()
    {
        Nav.MenuItems.Add(new NavigationViewItem { Content = "Home", Tag = "home", Icon = new FontIcon { Glyph = "" } });
        Nav.MenuItems.Add(new NavigationViewItemHeader { Content = "Tools" });
        foreach (IFluxTool tool in _host.Registry.All)
        {
            var item = new NavigationViewItem
            {
                Content = tool.Descriptor.DisplayName,
                Tag = tool.Descriptor.Id,
                Icon = ToolIcon(tool.Descriptor),
            };
            _toolItems[tool.Descriptor.Id] = item;
            Nav.MenuItems.Add(item);
        }

        OnStatusChanged(this, EventArgs.Empty);
    }

    /// <summary>The tool's full-colour icon when it has one, otherwise its Fluent glyph.</summary>
    public static IconElement ToolIcon(ToolDescriptor descriptor) => descriptor.IconAsset is { } asset
        ? new BitmapIcon { UriSource = new Uri($"ms-appx:///Assets/{asset}"), ShowAsMonochrome = false }
        : new FontIcon { Glyph = descriptor.Glyph };

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem { Tag: string tag })
        {
            return;
        }

        Type page = tag switch
        {
            "home" => typeof(HomePage),
            "general" => typeof(GeneralPage),
            "about" => typeof(AboutPage),
            _ => ToolPages.GetValueOrDefault(tag, typeof(HomePage)),
        };
        if (ContentFrame.CurrentSourcePageType != page)
        {
            ContentFrame.Navigate(page, null, new EntranceNavigationTransitionInfo());
        }
    }

    private void OnStatusChanged(object? sender, EventArgs e)
    {
        foreach ((string id, NavigationViewItem item) in _toolItems)
        {
            ToolState state = _host.Registry.Get(id)!.State;
            string? style = state.Status switch
            {
                ToolStatus.Running => "SuccessDotInfoBadgeStyle",
                ToolStatus.Degraded => "CautionDotInfoBadgeStyle",
                ToolStatus.Faulted => "CriticalDotInfoBadgeStyle",
                _ => null,
            };
            item.InfoBadge = style is null ? null : new InfoBadge { Style = (Style)Application.Current.Resources[style] };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetItemStatus(item, state.Status.ToString());
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ApplyTheme();

    private void ApplyTheme()
    {
        RootGrid.RequestedTheme = _host.AppTheme;
        UpdateCaptionButtons();
    }

    private void UpdateCaptionButtons()
    {
        AppWindowTitleBar bar = AppWindow.TitleBar;
        bool dark = RootGrid.ActualTheme == ElementTheme.Dark;
        bar.ButtonBackgroundColor = Colors.Transparent;
        bar.ButtonInactiveBackgroundColor = Colors.Transparent;
        bar.ButtonForegroundColor = dark ? Colors.White : Colors.Black;
        bar.ButtonInactiveForegroundColor = dark ? Windows.UI.Color.FromArgb(255, 150, 150, 150) : Windows.UI.Color.FromArgb(255, 110, 110, 110);
        bar.ButtonHoverBackgroundColor = dark ? Windows.UI.Color.FromArgb(25, 255, 255, 255) : Windows.UI.Color.FromArgb(25, 0, 0, 0);
        bar.ButtonHoverForegroundColor = bar.ButtonForegroundColor;
    }

    private void RestorePlacement()
    {
        WindowPlacement placement = _host.Settings.Read<GlobalSettings>(GlobalSettings.Section).MainWindow;
        double scale = WindowHelpers.GetDpi(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = (int)(760 * scale);
            presenter.PreferredMinimumHeight = (int)(520 * scale);
        }

        AppWindow.Resize(new SizeInt32((int)(placement.Width * scale), (int)(placement.Height * scale)));
        if (placement.X is int x && placement.Y is int y && DisplayArea.GetFromPoint(new PointInt32(x + 40, y + 20), DisplayAreaFallback.None) is not null)
        {
            AppWindow.Move(new PointInt32(x, y));
        }
        else
        {
            DisplayArea area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            AppWindow.Move(new PointInt32(
                area.WorkArea.X + ((area.WorkArea.Width - AppWindow.Size.Width) / 2),
                area.WorkArea.Y + ((area.WorkArea.Height - AppWindow.Size.Height) / 2)));
        }

        if (placement.Maximized && AppWindow.Presenter is OverlappedPresenter maximizable)
        {
            maximizable.Maximize();
        }
    }

    private async void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (!_firstActivation)
        {
            return;
        }

        _firstActivation = false;
        if (!_host.Settings.Read<GlobalSettings>(GlobalSettings.Section).FirstRunCompleted)
        {
            await Task.Delay(300); // let the window finish its entrance before the dialog
            await ShowFirstRunAsync();
        }
    }

    private async Task ShowFirstRunAsync()
    {
        var startup = new CheckBox { Content = "Start FluxKit when I sign in", Margin = new Thickness(0, 12, 0, 0) };
        var body = new StackPanel { Spacing = 8, MaxWidth = 420 };
        body.Children.Add(new TextBlock { Text = "FluxKit is a set of small tools that make Windows easier to use. Each tool can be turned on or off on its own.", TextWrapping = TextWrapping.Wrap });
        body.Children.Add(new TextBlock { Text = "Your first tool, Drag-to-Switch, is already on. Try it:", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
        string[] steps =
        [
            "1.  Start dragging a file in File Explorer.",
            "2.  Hold it over a desktop in the bar that appears.",
            "3.  Drop it into any app on that desktop.",
        ];
        foreach (string step in steps)
        {
            body.Children.Add(new TextBlock { Text = step, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
        }

        body.Children.Add(new TextBlock
        {
            Text = "FluxKit runs quietly in the system tray. It never copies, moves or uploads your files.",
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Margin = new Thickness(0, 8, 0, 0),
        });
        body.Children.Add(startup);

        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            RequestedTheme = RootGrid.ActualTheme,
            Title = "Welcome to FluxKit",
            Content = body,
            PrimaryButtonText = "Get started",
            SecondaryButtonText = "Drag-to-Switch settings",
            DefaultButton = ContentDialogButton.Primary,
        };
        ContentDialogResult result = await dialog.ShowAsync();

        bool launchAtStartup = startup.IsChecked == true;
        if (launchAtStartup)
        {
            _host.Startup.Enable();
        }

        await _host.Settings.UpdateAsync<GlobalSettings>(GlobalSettings.Section, s =>
        {
            s.FirstRunCompleted = true;
            s.LaunchAtStartup = launchAtStartup;
            return s;
        });
        if (result == ContentDialogResult.Secondary)
        {
            NavigateTo(DragToSwitchTool.Id);
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _host.ThemeChanged -= OnThemeChanged;
        _host.StatusChanged -= OnStatusChanged;
        double scale = WindowHelpers.GetDpi(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        bool maximized = AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized };
        PointInt32 position = AppWindow.Position;
        SizeInt32 size = AppWindow.Size;
        _ = _host.Settings.UpdateAsync<GlobalSettings>(GlobalSettings.Section, s =>
        {
            s.MainWindow.Maximized = maximized;
            if (!maximized)
            {
                s.MainWindow.X = position.X;
                s.MainWindow.Y = position.Y;
                s.MainWindow.Width = (int)(size.Width / scale);
                s.MainWindow.Height = (int)(size.Height / scale);
            }

            return s;
        });
    }
}
