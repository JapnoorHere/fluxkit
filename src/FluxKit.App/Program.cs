using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace FluxKit.App;

/// <summary>Custom entry point: enforces a single instance before any UI is created.</summary>
public static class Program
{
    public const string InstanceKey = "FluxKit.Main";

    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        AppInstance instance = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (!instance.IsCurrent)
        {
            // Another FluxKit is running: hand it our activation (it opens its window) and exit.
            AppActivationArguments activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            using var done = new ManualResetEventSlim(false);
            _ = Task.Run(async () =>
            {
                try
                {
                    await instance.RedirectActivationToAsync(activation);
                }
                finally
                {
                    done.Set();
                }
            });
            done.Wait(TimeSpan.FromSeconds(5));
            return 0;
        }

        Application.Start(callback =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App(args);
        });
        return 0;
    }
}
