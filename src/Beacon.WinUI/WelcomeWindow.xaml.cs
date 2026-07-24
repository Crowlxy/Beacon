using Beacon.Core;
using Beacon.Platform.Windows;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Beacon.WinUI;

public sealed partial class WelcomeWindow : Window
{
    private readonly Action _showLauncher;

    public WelcomeWindow(bool everythingAvailable, Action showLauncher)
    {
        _showLauncher = showLauncher;
        InitializeComponent();
        HotkeyNotice.Text = $"{R1Storage.Get("GlobalHotkey", "Alt+Shift+Space")} でいつでも検索";
        EverythingNotice.Visibility = everythingAvailable ? Visibility.Collapsed : Visibility.Visible;
        StartupToggle.IsOn = StartupRegistration.IsEnabled();
        ApplyAppearance(R1Storage.Get("Appearance", "System"));
        ConfigureBackdrop();
        CenterOnCurrentDisplay();
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "App.ico");
        if (File.Exists(iconPath)) AppWindow.SetIcon(iconPath);
    }

    /// <summary>設定画面と同じく、アプリ内の素材はThin Acrylicに揃える（不透明度は既定値のまま）。</summary>
    private void ConfigureBackdrop()
    {
        if (DesktopAcrylicController.IsSupported())
        {
            try
            {
                SystemBackdrop = new ThinDesktopAcrylicBackdrop(useCustomTuning: false);
                R1Storage.WriteLog("INFO Welcome backdrop path: thin desktop acrylic");
                return;
            }
            catch (Exception exception)
            {
                R1Storage.WriteLog($"WARN Welcome thin desktop acrylic unavailable: {exception.Message}");
            }
        }

        SystemBackdrop = null;
        Root.Background = (Brush)Application.Current.Resources["LauncherFallbackBrush"];
        R1Storage.WriteLog("WARN Welcome backdrop path: solid fallback");
    }

    private void CenterOnCurrentDisplay()
    {
        NativeMethods.GetCursorPos(out var cursor);
        var display = DisplayArea.GetFromPoint(
            new PointInt32(cursor.X, cursor.Y),
            DisplayAreaFallback.Nearest);
        var dpi = MonitorDpi(cursor);
        var width = Pixels((double)Application.Current.Resources["WelcomeWindowWidth"], dpi);
        var height = Pixels((double)Application.Current.Resources["WelcomeWindowHeight"], dpi);
        AppWindow.MoveAndResize(new RectInt32(
            display.WorkArea.X + ((display.WorkArea.Width - width) / 2),
            display.WorkArea.Y + ((display.WorkArea.Height - height) / 2),
            width,
            height));
    }

    private static int Pixels(double dip, uint dpi) => (int)Math.Round(dip * dpi / 96d);

    private static uint MonitorDpi(NativeMethods.Point point)
    {
        var monitor = NativeMethods.MonitorFromPoint(point, 2);
        return NativeMethods.GetDpiForMonitor(monitor, 0, out var x, out _) == 0 ? x : 96;
    }

    private void ApplyAppearance(string appearance)
    {
        Root.RequestedTheme = appearance switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }

    private void OnStartupToggled(object sender, RoutedEventArgs args)
    {
        try { StartupRegistration.SetEnabled(StartupToggle.IsOn, Environment.ProcessPath!); }
        catch (Exception exception) { R1Storage.WriteLog($"ERROR Startup registration failed: {exception.Message}"); }
    }

    private void OnTryBeacon(object sender, RoutedEventArgs args)
    {
        try
        {
            StartupRegistration.SetEnabled(StartupToggle.IsOn, Environment.ProcessPath!);
            R1Storage.WriteLog($"INFO Startup registration enabled={StartupToggle.IsOn}");
        }
        catch (Exception exception)
        {
            R1Storage.WriteLog($"ERROR Startup registration failed: {exception.Message}");
        }
        Close();
        _showLauncher();
    }
}