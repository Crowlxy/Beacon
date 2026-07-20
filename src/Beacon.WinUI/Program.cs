using Beacon.Core;
using System.Text;
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
            var signaled = SingleInstanceCoordinator.SignalExistingAsync().GetAwaiter().GetResult();
            WriteSecondaryInstanceLog(signaled);
            return signaled ? 0 : 3;
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

    private static void WriteSecondaryInstanceLog(bool signaled)
    {
        try
        {
            var resolution = DataRootResolver.Resolve(
                AppContext.BaseDirectory,
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            // beacon.log はプライマリが書く。FileMode.Append はアトミックでないため
            // 別プロセスから同一ファイルへ書くと行が破損する（LESSONS 2026-07-17）
            var path = Path.Combine(resolution.Path, "Logs", "beacon-secondary.log");
            using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.WriteLine($"{DateTimeOffset.Now:O} INFO Secondary instance signaled={signaled}, exiting");
        }
        catch (DataRootResolutionException)
        {
        }
        catch (IOException)
        {
        }
    }
}
