using Beacon.Core;
using System.Diagnostics;
using Microsoft.UI.Xaml;

namespace Beacon.WinUI;

public partial class App : Application, IDisposable
{
    private MainWindow? _window;
    private WelcomeWindow? _welcomeWindow;
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
            RunLegacyMigration(resolution.Path);
            Beacon.Platform.Windows.StartupRegistration.EnsureCurrentPath(Environment.ProcessPath!, R1Storage.WriteLog);
            _window = new MainWindow(resolution.Path);
            Program.Instance!.StartListening(_window.ShowLauncher);
            R1Storage.WriteLog("INFO Single-instance activation pipe listening");
            if (Environment.GetEnvironmentVariable("BEACON_SMOKE_SETTINGS") == "1") _window.ShowSettings();
            R1Storage.WriteLog($"PERF StartupToResidentMs={Stopwatch.GetElapsedTime(Program.StartedTimestamp).TotalMilliseconds:F1}");
            _ = ShowWelcomeIfNeededAsync();

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
        catch (Exception exception)
        {
            R1Storage.WriteLog($"ERROR Startup failure: {exception}");
            _ = NativeMethods.MessageBoxW(
                IntPtr.Zero,
                $"Beaconの起動中に予期しないエラーが発生しました。\n{exception.Message}",
                "Beacon startup error",
                NativeMethods.MessageBoxIconError);
            Exit();
        }
    }

    private async Task ShowWelcomeIfNeededAsync()
    {
        if (_window is null || R1Storage.GetBoolean("WelcomeShown", false)) return;
        var availability = await Beacon.Platform.Windows.Everything.EverythingApi.GetAvailabilityAsync(CancellationToken.None);
        if (R1Storage.GetBoolean("WelcomeShown", false)) return;
        R1Storage.SetBoolean("WelcomeShown", true);
        _welcomeWindow = new WelcomeWindow(availability.Available, _window.ShowLauncher);
        _welcomeWindow.Activate();
        R1Storage.WriteLog("INFO First-run welcome displayed");
    }
    private static void RunLegacyMigration(string dataRoot)
    {
        if (Environment.GetEnvironmentVariable("BEACON_SKIP_LEGACY_MIGRATION") == "1") return;
        if (LegacyImporter.WasAttempted(dataRoot)) return;
        var candidate = LegacyImporter.Detect(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppContext.BaseDirectory);
        if (candidate is null) return;
        var confirmed = NativeMethods.MessageBoxW(
            IntPtr.Zero,
            $"旧Beaconデータを検出しました。\n移行元: {candidate.SourceRoot}\n移行先: {dataRoot}\n\nバックアップして移行しますか？",
            "Beacon データ移行",
            NativeMethods.MessageBoxYesNoWarning) == NativeMethods.MessageBoxResultYes;
        LegacyMigrationResult result;
        if (confirmed) result = LegacyImporter.Import(candidate, dataRoot);
        else
        {
            LegacyImporter.RecordDeclined(candidate, dataRoot);
            result = new(false, "移行しませんでした。");
        }
        _ = NativeMethods.MessageBoxW(IntPtr.Zero, result.Message, "Beacon データ移行", result.Success ? 0u : NativeMethods.MessageBoxIconError);
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
