using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Beacon.WinUI;

internal sealed class ThinDesktopAcrylicBackdrop : SystemBackdrop, IDisposable
{
    private readonly bool _useCustomTuning;
    private DesktopAcrylicController? _controller;
    private SystemBackdropConfiguration? _configuration;
    private FrameworkElement? _themeRoot;

    private readonly string _tintOpacityKey;
    private readonly string _luminosityOpacityKey;

    /// <param name="useCustomTuning">
    /// true のときだけ <paramref name="tintOpacityKey"/> / <paramref name="luminosityOpacityKey"/> の独自値を使う。
    /// false では <see cref="DesktopAcrylicKind.Thin"/> の既定値をそのまま使い、テーマごとの再導出もフレームワークに委ねる。
    /// </param>
    internal ThinDesktopAcrylicBackdrop(
        bool useCustomTuning = true,
        string tintOpacityKey = "AcrylicTintOpacity",
        string luminosityOpacityKey = "AcrylicLuminosityOpacity")
    {
        _useCustomTuning = useCustomTuning;
        _tintOpacityKey = tintOpacityKey;
        _luminosityOpacityKey = luminosityOpacityKey;
    }

    internal void SetInputActive(bool active)
    {
        if (_configuration is not null) _configuration.IsInputActive = active;
    }

    protected override void OnTargetConnected(
        ICompositionSupportsSystemBackdrop connectedTarget,
        XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);
        if (_controller is not null)
            throw new InvalidOperationException("This backdrop cannot be shared.");

        var controller = new DesktopAcrylicController { Kind = DesktopAcrylicKind.Thin };
        if (_useCustomTuning)
        {
            // 代入した時点でテーマ変更時の自動再導出が止まる点に注意（LESSONS.md 2026-07-24）。
            // ランチャーは両テーマで同じ薄さを意図しているため、承知のうえで固定している。
            controller.TintOpacity = (float)(double)Application.Current.Resources[_tintOpacityKey];
            controller.LuminosityOpacity = (float)(double)Application.Current.Resources[_luminosityOpacityKey];
        }
        try
        {
            _themeRoot = xamlRoot.Content as FrameworkElement;
            _configuration = new SystemBackdropConfiguration
            {
                IsInputActive = true,
                Theme = ThemeFor(_themeRoot?.ActualTheme ?? ElementTheme.Default),
            };
            if (_themeRoot is not null) _themeRoot.ActualThemeChanged += OnActualThemeChanged;
            controller.SetSystemBackdropConfiguration(_configuration);
            controller.AddSystemBackdropTarget(connectedTarget);
            _controller = controller;
        }
        catch
        {
            if (_themeRoot is not null) _themeRoot.ActualThemeChanged -= OnActualThemeChanged;
            _themeRoot = null;
            _configuration = null;
            controller.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 既定のSystemBackdropConfigurationは使わず自前の構成で制御しているため、既定構成の変更通知は握りつぶす。
    /// Windows設定でテーマを切り替えるとこの通知が届き、基底実装がE_INVALIDARGを投げてアプリが落ちていた。
    /// テーマ追従は OnActualThemeChanged で行う。
    /// </summary>
    protected override void OnDefaultSystemBackdropConfigurationChanged(
        ICompositionSupportsSystemBackdrop target,
        XamlRoot xamlRoot)
    {
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        base.OnTargetDisconnected(disconnectedTarget);
        _controller?.RemoveSystemBackdropTarget(disconnectedTarget);
        Dispose();
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        if (_configuration is null) return;
        try
        {
            _configuration.Theme = ThemeFor(sender.ActualTheme);
        }
        catch (Exception exception)
        {
            R1Storage.WriteLog($"WARN Launcher Acrylic theme update failed: {exception.Message}");
        }
    }

    private static SystemBackdropTheme ThemeFor(ElementTheme theme) => theme switch
    {
        ElementTheme.Light => SystemBackdropTheme.Light,
        ElementTheme.Dark => SystemBackdropTheme.Dark,
        _ => SystemBackdropTheme.Default,
    };

    public void Dispose()
    {
        if (_themeRoot is not null) _themeRoot.ActualThemeChanged -= OnActualThemeChanged;
        _themeRoot = null;
        _configuration = null;
        _controller?.Dispose();
        _controller = null;
        GC.SuppressFinalize(this);
    }
}