using FluxKit.App.Controls;
using FluxKit.App.Modules.DragToSwitch;
using FluxKit.App.Theming;
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

/// <summary>The settings window: Mica, custom title bar, a navigation rail built from the tool registry.</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Settings page for each tool id. Adding a tool adds one entry here.</summary>
    private static readonly Dictionary<string, Type> ToolPages = new()
    {
        [DragToSwitchTool.Id] = typeof(DragToSwitchPage),
    };

    private readonly AppHost _host = AppHost.Current;
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
        BuildRail();
        ApplyTheme();

        _host.ThemeChanged += OnThemeChanged;
        _host.StatusChanged += OnStatusChanged;
        Activated += OnActivated;
        Closed += OnClosed;
        RootGrid.ActualThemeChanged += (_, _) => UpdateCaptionButtons();
        Rail.Select("home");
    }

    public void NavigateTo(string tag) => Rail.Select(tag);

    public void BringToFront()
    {
        nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
        {
            presenter.Restore();
        }

        WindowHelpers.BringToForeground(hwnd);
    }

    /// <summary>The tool's full-colour icon when it has one, otherwise its Fluent glyph.</summary>
    public static IconElement ToolIcon(ToolDescriptor descriptor) => descriptor.IconAsset is { } asset
        ? new BitmapIcon { UriSource = new Uri($"ms-appx:///Assets/{asset}"), ShowAsMonochrome = false }
        : new FontIcon { Glyph = descriptor.Glyph };

    private void BuildRail()
    {
        Rail.AddItem("home", Strings.Get("Rail_Home"), Strings.Get("Rail_Home"), new FontIcon { Glyph = "" });
        foreach (IFluxTool tool in _host.Registry.All)
        {
            ToolDescriptor d = tool.Descriptor;
            Rail.AddItem(d.Id, d.DisplayName, d.DisplayName, ToolIcon(d));
        }

        Rail.AddItem("general", Strings.Get("Rail_General"), Strings.Get("Rail_General"), new FontIcon { Glyph = "" }, atBottom: true);
        Rail.AddItem("about", Strings.Get("Rail_About"), Strings.Get("Rail_About"), new FontIcon { Glyph = "" }, atBottom: true);
        Rail.ItemInvoked += (_, tag) => ShowPage(tag);
        OnStatusChanged(this, EventArgs.Empty);
    }

    private void ShowPage(string tag)
    {
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
        foreach (IFluxTool tool in _host.Registry.All)
        {
            Rail.SetStatus(tool.Descriptor.Id, StatusTones.For(tool.State.Status), tool.State.Status.ToString());
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
        try
        {
            if (!_host.Settings.Read<GlobalSettings>(GlobalSettings.Section).FirstRunCompleted)
            {
                await Task.Delay(300); // let the window finish its entrance before the dialog
                await ShowFirstRunAsync();
            }
        }
        catch (Exception ex)
        {
            _host.Log.Error(ex, "First-run dialog failed");
        }
    }

    private async Task ShowFirstRunAsync()
    {
        var startup = new CheckBox { Content = Strings.Get("FirstRun_Startup"), Margin = new Thickness(0, 12, 0, 0) };
        var body = new StackPanel { Spacing = 8, MaxWidth = 420 };
        body.Children.Add(new TextBlock { Text = Strings.Get("FirstRun_Intro"), TextWrapping = TextWrapping.Wrap });
        body.Children.Add(new TextBlock { Text = Strings.Get("FirstRun_TryIt"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
        foreach (string key in new[] { "FirstRun_Step1", "FirstRun_Step2", "FirstRun_Step3" })
        {
            body.Children.Add(new TextBlock { Text = Strings.Get(key), TextWrapping = TextWrapping.Wrap, Foreground = ThemeResources.TextSecondary });
        }

        body.Children.Add(new TextBlock
        {
            Text = Strings.Get("FirstRun_Privacy"),
            TextWrapping = TextWrapping.Wrap,
            Style = ThemeResources.CaptionText,
            Margin = new Thickness(0, 8, 0, 0),
        });
        body.Children.Add(startup);

        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            RequestedTheme = RootGrid.ActualTheme,
            Title = Strings.Get("FirstRun_Title"),
            Content = body,
            PrimaryButtonText = Strings.Get("FirstRun_Primary"),
            SecondaryButtonText = Strings.Get("FirstRun_Secondary"),
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
