using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace FluxKit.App.Shell;

/// <summary>Version, environment, support links and the privacy statement.</summary>
public sealed partial class AboutPage : Page
{
    private readonly AppHost _host = AppHost.Current;

    public AboutPage()
    {
        InitializeComponent();
        Header.StatusText = Strings.Format("About_VersionFormat", _host.Version, System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture);
        OsRow.Description = Diagnostics.OsDescription;
        BackendRow.Description = Strings.Format("About_BackendFormat", _host.Desktops.BackendDescription, _host.Desktops.Desktops.Count);
        SettingsRow.Description = _host.SettingsPath;
        LogsRow.Description = _host.LogDirectory;
        CopyButton.Content = Strings.Get("About_Copy");
    }

    private async void OnCopyDiagnostics(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(Diagnostics.Build(_host));
        Clipboard.SetContent(package);
        CopyButton.Content = Strings.Get("Common_Copied");
        await Task.Delay(2000);
        CopyButton.Content = Strings.Get("About_Copy");
    }

    private void OnOpenDocs(object sender, RoutedEventArgs e) => AppHost.OpenFolder(Path.Combine(AppContext.BaseDirectory, "Docs"));

    private void OnOpenNotices(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "THIRD-PARTY-NOTICES.md");
        if (File.Exists(path))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
    }
}
