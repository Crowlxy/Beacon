using System.Diagnostics;
using System.IO.Pipes;
using Beacon.Contracts;
using Beacon.Core;
using Beacon.Platform.Windows;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using StreamJsonRpc;
using Windows.Graphics;

namespace Beacon.WinUI;

public sealed partial class MainWindow : Window, IDisposable
{
    private readonly string _dataRoot;
    private readonly AppWindow _appWindow;
    private readonly NativeWindowController _nativeWindow;
    private readonly UsageHistoryStore _usageHistory;
    private readonly ClipboardHistoryService _clipboardHistory;
    private readonly FileSearchProvider _fileSearchProvider;
    private readonly BrowserBookmarkProvider _bookmarkProvider;
    private bool _exiting;

    public MainWindow(string dataRoot)
    {
        _dataRoot = dataRoot;
        _appSearchProvider = new AppSearchProvider();
        _fileSearchProvider = new FileSearchProvider(R1Storage.WriteLog);
        _bookmarkProvider = new BrowserBookmarkProvider();
        _usageHistory = new UsageHistoryStore(dataRoot, R1Storage.WriteLog);
        _usageHistory.Enabled = R1Storage.GetBoolean("PersonalizationEnabled", true);
        _orchestrator = new QueryOrchestrator(
            [
                _appSearchProvider,
                _fileSearchProvider,
                new CalculatorSearchProvider(),
                new UrlSearchProvider(),
                new WebSearchProvider(),
                new WindowsSettingsProvider(),
                _bookmarkProvider,
                new SystemActionSearchProvider(),
                new ShellSearchProvider(),
                new ProcessKillerSearchProvider(),
            ],
            _usageHistory,
            log: R1Storage.WriteLog);
        R1Storage.WriteLog("INFO MainWindow initialization started");
        InitializeComponent();
        ConfigureBackdrop();
        R1Storage.WriteLog("INFO MainWindow XAML initialized");
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        R1Storage.WriteLog("INFO MainWindow handle acquired");
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _appWindow.Closing += OnClosing;
        R1Storage.WriteLog("INFO AppWindow acquired");
        _clipboardHistory = new ClipboardHistoryService(windowHandle, dataRoot, R1Storage.WriteLog);
        if (R1Storage.GetBoolean("ClipboardEnabled", false)) _clipboardHistory.SetEnabled(true);
        _nativeWindow = new NativeWindowController(
            windowHandle,
            ShowLauncher,
            RepositionLauncher,
            ExitApplication,
            () => _clipboardHistory.Enabled,
            ToggleClipboardHistory,
            () => _usageHistory.Enabled,
            TogglePersonalization,
            ResetPersonalization,
            _clipboardHistory.OnClipboardUpdate);
        InitializeLauncher(windowHandle);
        _ = RefreshAppCacheAsync();
        _bookmarkProvider.Preload();
        R1Storage.WriteLog("INFO Hotkey and tray registered");
    }

    private void ConfigureBackdrop()
    {
        if (DesktopAcrylicController.IsSupported())
        {
            try
            {
                SystemBackdrop = new ThinDesktopAcrylicBackdrop();
                R1Storage.WriteLog("INFO Backdrop path: thin desktop acrylic");
                return;
            }
            catch (Exception exception)
            {
                R1Storage.WriteLog($"WARN Thin desktop acrylic unavailable: {exception.Message}");
            }
        }

        try
        {
            SystemBackdrop = new DesktopAcrylicBackdrop();
            R1Storage.WriteLog("INFO Backdrop path: standard desktop acrylic fallback");
        }
        catch (Exception exception)
        {
            SystemBackdrop = null;
            Panel.Background = (Brush)Application.Current.Resources["LauncherFallbackBrush"];
            R1Storage.WriteLog($"WARN Backdrop path: solid fallback; desktop acrylic unavailable: {exception.Message}");
        }
    }

    public void ShowLauncher()
    {
        DispatcherQueue.TryEnqueue(ShowLauncherCore);
    }

    public async Task RunRpcSpikeAsync()
    {
        R1Storage.WriteLog("INFO RPC spike started");
        try
        {
            await RunRpcSpikeCoreAsync();
        }
        catch (Exception exception)
        {
            R1Storage.WriteLog($"ERROR RPC spike failed: {exception.Message}");
        }
    }

    private async Task RunRpcSpikeCoreAsync()
    {
        var executable = Path.Combine(AppContext.BaseDirectory, "Beacon.PluginHost.exe");
        if (!File.Exists(executable))
        {
            R1Storage.WriteLog("ERROR Beacon.PluginHost.exe was not found");
            return;
        }

        var pipeName = $"Beacon.Next.PluginHost.{Guid.NewGuid():N}";
        using var process = Process.Start(new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            ArgumentList = { "--pipe", pipeName, "--data-root", _dataRoot },
        });
        if (process is null)
        {
            R1Storage.WriteLog("ERROR Beacon.PluginHost.exe could not start");
            return;
        }

        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(5000);
            using var rpc = new JsonRpc(new HeaderDelimitedMessageHandler(pipe, pipe, new SystemTextJsonFormatter()));
            using var cancellation = new CancellationTokenSource();
            rpc.AddLocalRpcTarget(new SearchResultReceiver(cancellation));
            rpc.StartListening();

            try
            {
                await rpc.InvokeWithParameterObjectAsync(
                    "search",
                    new SearchRequest(Guid.NewGuid().ToString("N"), "beacon", QueryScope.All, ContractVersion.Current),
                    cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                R1Storage.WriteLog("INFO RPC cancellation confirmed after incremental results");
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }

            await process.WaitForExitAsync();
        }
    }

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!_exiting)
        {
            args.Cancel = true;
            sender.Hide();
        }
    }

    private void ExitApplication()
    {
        _exiting = true;
        R1Storage.MarkCleanExit();
        Dispose();
        Close();
        Application.Current.Exit();
    }

    private void ToggleClipboardHistory()
    {
        _clipboardHistory.SetEnabled(!_clipboardHistory.Enabled);
        R1Storage.SetBoolean("ClipboardEnabled", _clipboardHistory.Enabled);
        R1Storage.WriteLog($"INFO Clipboard history enabled={_clipboardHistory.Enabled}");
    }

    private void TogglePersonalization()
    {
        _usageHistory.Enabled = !_usageHistory.Enabled;
        R1Storage.SetBoolean("PersonalizationEnabled", _usageHistory.Enabled);
        R1Storage.WriteLog($"INFO Personalization enabled={_usageHistory.Enabled}");
    }

    private void ResetPersonalization()
    {
        if (NativeMethods.MessageBoxW(_windowHandle, "個人化データをすべて削除しますか？", "Beacon", NativeMethods.MessageBoxYesNoWarning) != NativeMethods.MessageBoxResultYes) return;
        _usageHistory.Reset();
        R1Storage.WriteLog("INFO Personalization data reset");
    }

    public void Dispose()
    {
        _runningCancellation?.Cancel();
        _runningCancellation?.Dispose();
        _orchestrator.Dispose();
        _clipboardHistory.Dispose();
        _fileSearchProvider.Dispose();
        _appSearchProvider.Dispose();
        _nativeWindow.Dispose();
    }

    private sealed class SearchResultReceiver(CancellationTokenSource cancellation)
    {
        private int _count;

        [JsonRpcMethod("searchResult", UseSingleObjectParameterDeserialization = true)]
        public void OnSearchResult(SearchResultDto result)
        {
            if (string.IsNullOrWhiteSpace(result.Id))
            {
                return;
            }

            R1Storage.WriteLog($"INFO RPC incremental result {result.Id}");
            if (Interlocked.Increment(ref _count) == 2)
            {
                cancellation.Cancel();
            }
        }
    }
}
