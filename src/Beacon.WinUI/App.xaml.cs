using Beacon.Core;
using Microsoft.UI.Xaml;

namespace Beacon.WinUI;

public partial class App : Application, IDisposable
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, eventArgs) =>
            R1Storage.WriteLog($"ERROR Unhandled WinUI failure: {eventArgs.Message}");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var resolution = DataRootResolver.Resolve(
                AppContext.BaseDirectory,
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            R1Storage.Initialize(resolution.Path);
            _window = new MainWindow(resolution.Path);
            Program.Instance!.StartListening(_window.ShowLauncher);
            R1Storage.WriteLog("INFO Single-instance activation pipe listening");

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
        GC.SuppressFinalize(this);
    }
}
