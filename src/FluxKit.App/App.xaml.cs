using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Serilog;

namespace FluxKit.App;

public partial class App : Application
{
    private readonly string[] _args;
    private AppHost? _host;

    public App(string[] args)
    {
        _args = args;
        InitializeComponent();

        // FluxKit lives in the tray: closing the settings window must not end the process.
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _host = new AppHost(DispatcherQueue.GetForCurrentThread());
            await _host.StartAsync(_args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "FluxKit failed to start");
            await Log.CloseAndFlushAsync();
            Exit();
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled UI exception");
        e.Handled = true; // keep the tray app alive; the error is logged for diagnostics
    }
}
