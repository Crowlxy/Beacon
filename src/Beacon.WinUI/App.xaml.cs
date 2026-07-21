using Beacon.Core;
using System.Diagnostics;
using Microsoft.UI.Xaml;

namespace Beacon.WinUI;

public partial class App : Application, IDisposable
{
    private MainWindow? _window;
    private bool _terminating;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, eventArgs) => TerminateAfterFailure("WinUI", eventArgs.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            TerminateAfterFailure("AppDomain", eventArgs.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            R1Storage.WriteLog($"ERROR Unobserved task failure: {eventArgs.Exception}");
            eventArgs.SetObserved();
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var resolution = DataRootResolver.Resolve(
                AppContext.BaseDirectory,
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            R1Storage.Initialize(resolution.Path);
            Beacon.Platform.Windows.StartupRegistration.EnsureCurrentPath(Environment.ProcessPath!, R1Storage.WriteLog);
            _window = new MainWindow(resolution.Path);
            Program.Instance!.StartListening(_window.ShowLauncher);
            R1Storage.WriteLog("INFO Single-instance activation pipe listening");
            R1Storage.WriteLog($"PERF StartupToResidentMs={Stopwatch.GetElapsedTime(Program.StartedTimestamp).TotalMilliseconds:F1}");

        }
        catch (DataRootResolutionException exception)
        {
            _ = NativeMethods.MessageBoxW(
                IntPtr.Zero,
                exception.Message,
                "Beacon portable data error",
                NativeMethods.MessageBoxIconError);
            Exit();
        }
    }

    public void Dispose()
    {
        _window?.Dispose();
        if (!_terminating) R1Storage.MarkCleanExit();
        GC.SuppressFinalize(this);
    }

    private void TerminateAfterFailure(string source, Exception? exception)
    {
        if (_terminating) return;
        _terminating = true;
        R1Storage.WriteLog($"ERROR Unhandled {source} failure: {exception}");
        _window?.Dispose();
        Exit();
    }
}
