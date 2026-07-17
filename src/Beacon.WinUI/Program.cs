using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Beacon.WinUI;

internal static class Program
{
    public static SingleInstanceCoordinator? Instance { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--probe-data-root", StringComparer.Ordinal))
        {
            try
            {
                DataRootResolver.Resolve(
                    AppContext.BaseDirectory,
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
                return 0;
            }
            catch (DataRootResolutionException)
            {
                return 4;
            }
        }

        Instance = SingleInstanceCoordinator.TryAcquire();
        if (Instance is null)
        {
            return SingleInstanceCoordinator.SignalExistingAsync().GetAwaiter().GetResult() ? 0 : 3;
        }

        using (Instance)
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            App? app = null;
            Application.Start(initialization =>
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
                app = new App();
            });
            app?.Dispose();
        }

        return 0;
    }
}
