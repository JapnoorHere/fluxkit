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
        // FluxKit.exe island …: send a Live Activity update to the running FluxKit, then exit (no window).
        if (args.Length > 0 && string.Equals(args[0], "island", StringComparison.OrdinalIgnoreCase))
        {
            return LiveActivityCommand.Run(args[1..]);
        }

        WinRT.ComWrappersSupport.InitializeComWrappers();

        AppInstance instance = AppInstance.FindOrRegisterForKey(InstanceKey);

        // A restart (e.g. after registering the identity package) waits for the old instance to finish closing.
        for (int tries = 0; !instance.IsCurrent && args.Contains("--restarted") && tries < 50; tries++)
        {
            Thread.Sleep(100);
            instance = AppInstance.FindOrRegisterForKey(InstanceKey);
        }

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
