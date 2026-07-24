using System.Diagnostics;
using System.Reflection;
using Beacon.Contracts;
using Beacon.Core;
using Beacon.Platform.Windows;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Beacon.WinUI;

public sealed partial class SettingsWindow : Window, IDisposable
{
    public delegate bool ChangeHotkey(string value, out string error);
    private readonly Func<string, (bool Success, string Error)> _changeHotkey;
    private readonly Func<bool> _clipboardEnabled;
    private readonly Action _toggleClipboard;
    private readonly Func<bool> _personalizationEnabled;
    private readonly Action _togglePersonalization;
    private readonly Action _resetPersonalization;
    private readonly Action _clearClipboard;
    private readonly Action<string[]> _applyClipboardExclusions;
    private readonly Action<string> _applyAppearance;
    private bool _loading = true;
    private readonly QuickKeyRegistry _quickKeys = new(
        () => R1Storage.Get<Dictionary<string, string>?>("QuickKeys", null),
        mappings => R1Storage.Set("QuickKeys", new Dictionary<string, string>(mappings, StringComparer.OrdinalIgnoreCase)));

    public SettingsWindow(
        string dataRoot,
        ChangeHotkey changeHotkey,
        Func<bool> clipboardEnabled,
        Action toggleClipboard,
        Func<bool> personalizationEnabled,
        Action togglePersonalization,
        Action resetPersonalization,
        Action clearClipboard,
        Action<string[]> applyClipboardExclusions,
        Action<string> applyAppearance)
    {
        _changeHotkey = value =>
        {
            var success = changeHotkey(value, out var error);
            return (success, error);
        };
        _clipboardEnabled = clipboardEnabled;
        _toggleClipboard = toggleClipboard;
        _personalizationEnabled = personalizationEnabled;
        _togglePersonalization = togglePersonalization;
        _resetPersonalization = resetPersonalization;
        _clearClipboard = clearClipboard;
        _applyClipboardExclusions = applyClipboardExclusions;
        _applyAppearance = applyAppearance;
        InitializeComponent();
        var appearance = R1Storage.Get("Appearance", "System");
        AppearanceBox.SelectedValue = appearance;
        ApplyAppearance(appearance);
        ConfigureBackdrop();
        ConfigureIntegratedTitleBar();
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "App.ico");
        if (File.Exists(iconPath)) AppWindow.SetIcon(iconPath);
        AppWindow.Closing += (_, args) =>
        {
            args.Cancel = true;
            CloseToBackground();
        };
        Activated += OnSettingsActivated;
        ResizeForDpi();
        HotkeyBox.Text = R1Storage.Get("GlobalHotkey", "Alt+Shift+Space");
        StartupToggle.IsOn = StartupRegistration.IsEnabled();
        LoadQuickKeys();
        ClipboardToggle.IsOn = _clipboardEnabled();
        PersonalizationToggle.IsOn = _personalizationEnabled();
        LoadExcludedApps();
        SettingsNavigation.SelectedItem = SettingsNavigation.MenuItems[0];
        VersionText.Text = $"Version {Assembly.GetExecutingAssembly().GetName().Version}";
        ContractText.Text = $"ContractVersion {ContractVersion.Current}";
        _loading = false;
    }

    /// <summary>
    /// ランチャーと同じThin Acrylicを使い、アプリ内で素材を1種類に揃える。
    /// ナビ枠は素材そのまま、コンテンツ側はNavigationView標準のレイヤーが重なるので、
    /// 同じ素材のまま透け具合だけが二段階になる。
    /// 素材のTintOpacity/LuminosityOpacityは既定値のまま（useCustomTuning:false）とし、
    /// 「もう少し濃く」はXAML側で AppSurfaceScrimBrush を薄く重ねて実現する。
    /// 素材値を代入しないので、テーマ変更時の再導出はフレームワークに委ねられる（LESSONS.md 2026-07-24）。
    /// </summary>
    private void ConfigureBackdrop()
    {
        if (DesktopAcrylicController.IsSupported())
        {
            try
            {
                SystemBackdrop = new ThinDesktopAcrylicBackdrop(useCustomTuning: false);
                R1Storage.WriteLog("INFO Settings backdrop path: thin desktop acrylic");
                return;
            }
            catch (Exception exception)
            {
                R1Storage.WriteLog($"WARN Settings thin desktop acrylic unavailable: {exception.Message}");
            }
        }

        SystemBackdrop = null;
        SettingsRoot.Background = (Brush)Application.Current.Resources["LauncherFallbackBrush"];
        R1Storage.WriteLog("WARN Settings backdrop path: solid fallback");
    }

    /// <summary>
    /// Windows純正のキャプションバーを外し、UIに馴染む自前のタイトルバーへ差し替える。
    /// ドラッグはInputNonClientPointerSourceのCaption領域で行い、閉じるボタン部分は領域から除外する。
    /// </summary>
    private void ConfigureIntegratedTitleBar()
    {
        ExtendsContentIntoTitleBar = true;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
            presenter.IsMaximizable = false;
        }
        SettingsTitleBarDragRegion.SizeChanged += (_, _) => UpdateTitleBarDragRegion();
        UpdateTitleBarDragRegion();
    }

    private void UpdateTitleBarDragRegion()
    {
        var width = SettingsTitleBarDragRegion.ActualWidth;
        var height = SettingsTitleBarDragRegion.ActualHeight;
        if (width <= 0 || height <= 0) return;
        var scale = SettingsTitleBarDragRegion.XamlRoot?.RasterizationScale ?? 1d;
        try
        {
            InputNonClientPointerSource.GetForWindowId(AppWindow.Id).SetRegionRects(
                NonClientRegionKind.Caption,
                [new Windows.Graphics.RectInt32(0, 0, (int)Math.Round(width * scale), (int)Math.Round(height * scale))]);
        }
        catch (Exception exception)
        {
            R1Storage.WriteLog($"WARN Settings title bar drag region failed: {exception.Message}");
        }
    }

    /// <summary>
    /// 非アクティブ時はAcrylicのサンプリングを止めて、背面が動いても設定画面がちらつかないようにする。
    /// 再取得が要る状態の読み直しはアクティブになったときだけ行う。
    /// </summary>
    private void OnSettingsActivated(object sender, WindowActivatedEventArgs args)
    {
        if (SystemBackdrop is ThinDesktopAcrylicBackdrop acrylic)
            acrylic.SetInputActive(args.WindowActivationState != WindowActivationState.Deactivated);
        if (args.WindowActivationState == WindowActivationState.Deactivated) return;
        RefreshToggleState();
        _ = RefreshEverythingStatusAsync();
    }

    private void OnCloseSettings(object sender, RoutedEventArgs args) => CloseToBackground();

    /// <summary>閉じる操作は常に保存してから隠す（ウィンドウは常駐アプリの一部として再利用する）。</summary>
    private void CloseToBackground()
    {
        SaveQuickKeys();
        SaveExcludedApps();
        AppWindow.Hide();
    }

    private void ResizeForDpi()
    {
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var dpi = NativeMethods.GetDpiForWindow(windowHandle);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(
            Pixels((double)Application.Current.Resources["SettingsWindowWidth"], dpi),
            Pixels((double)Application.Current.Resources["SettingsWindowHeight"], dpi)));
    }

    private static int Pixels(double dip, uint dpi) => (int)Math.Round(dip * dpi / 96d);

    private void RefreshToggleState()
    {
        _loading = true;
        ClipboardToggle.IsOn = _clipboardEnabled();
        PersonalizationToggle.IsOn = _personalizationEnabled();
        StartupToggle.IsOn = StartupRegistration.IsEnabled();
        _loading = false;
    }

    private static void SetInlineError(TextBlock target, string message)
    {
        target.Text = message;
        target.Visibility = message.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnHotkeyKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key is VirtualKey.Control or VirtualKey.Menu or VirtualKey.Shift or VirtualKey.LeftWindows or VirtualKey.RightWindows) return;
        var modifiers = new List<string>();
        if (NativeMethods.ControlPressed()) modifiers.Add("Ctrl");
        if (NativeMethods.AltPressed()) modifiers.Add("Alt");
        if (NativeMethods.ShiftPressed()) modifiers.Add("Shift");
        if (NativeMethods.WindowsPressed()) modifiers.Add("Win");
        if (modifiers.Count == 0) { SetInlineError(HotkeyError, "修飾キーが必要です。"); return; }
        modifiers.Add(args.Key == VirtualKey.Space ? "Space" : args.Key.ToString());
        var value = string.Join("+", modifiers);
        var result = _changeHotkey(value);
        SetInlineError(HotkeyError, result.Success ? string.Empty : $"登録できません: {result.Error}");
        if (result.Success) HotkeyBox.Text = value;
        args.Handled = true;
    }

    private static Border CreateRowCard(Grid content)
    {
        content.ColumnSpacing = (double)Application.Current.Resources["SettingsItemSpacing"];
        return new Border
        {
            Style = (Style)Application.Current.Resources["SettingsCardStyle"],
            Child = content,
        };
    }

    private static Button CreateRemoveButton()
    {
        var button = new Button
        {
            Content = new FontIcon { Glyph = "\uE711", FontSize = (double)Application.Current.Resources["SettingsLabelSize"] },
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTipService.SetToolTip(button, "削除");
        AutomationProperties.SetName(button, "削除");
        return button;
    }

    private void LoadQuickKeys()
    {
        var wasLoading = _loading;
        _loading = true;
        QuickKeysRows.Children.Clear();
        foreach (var value in _quickKeys.Load()) AddQuickKeyRow(value.Key, value.Value);
        _loading = wasLoading;
    }

    private void AddQuickKeyRow(string key = "", string actionId = "open")
    {
        var keyBox = new TextBox
        {
            PlaceholderText = "キー",
            Text = key,
            Width = (double)Application.Current.Resources["SettingsQuickKeyBoxWidth"],
            FontFamily = (FontFamily)Application.Current.Resources["QuickKeyFontFamily"],
            VerticalAlignment = VerticalAlignment.Center,
        };
        var actionBox = new ComboBox
        {
            ItemsSource = BuiltInActions.All,
            DisplayMemberPath = nameof(ActionDescriptor.Title),
            SelectedValuePath = nameof(ActionDescriptor.Id),
            SelectedValue = actionId,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(keyBox, "Quick Key");
        AutomationProperties.SetName(actionBox, "アクション");
        var remove = CreateRemoveButton();
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(actionBox, 1);
        Grid.SetColumn(remove, 2);
        grid.Children.Add(keyBox);
        grid.Children.Add(actionBox);
        grid.Children.Add(remove);
        var card = CreateRowCard(grid);
        keyBox.LostFocus += (_, _) => SaveQuickKeys();
        actionBox.SelectionChanged += (_, _) => SaveQuickKeys();
        remove.Click += (_, _) => { QuickKeysRows.Children.Remove(card); SaveQuickKeys(); };
        QuickKeysRows.Children.Add(card);
    }

    private void OnAddQuickKey(object sender, RoutedEventArgs args) => AddQuickKeyRow();

    private void SaveQuickKeys()
    {
        if (_loading) return;
        var errorBrush = (Brush)Application.Current.Resources["LauncherErrorBrush"];
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var duplicated = false;
        foreach (var row in QuickKeysRows.Children.OfType<Border>().Select(border => (Grid)border.Child))
        {
            var keyBox = row.Children.OfType<TextBox>().Single();
            var key = keyBox.Text.Trim();
            var action = row.Children.OfType<ComboBox>().Single().SelectedValue as string;
            if (key.Length == 0 || action is null)
            {
                keyBox.ClearValue(Control.BorderBrushProperty);
                continue;
            }
            if (values.TryAdd(key, action))
            {
                keyBox.ClearValue(Control.BorderBrushProperty);
            }
            else
            {
                keyBox.BorderBrush = errorBrush;
                duplicated = true;
            }
        }
        SetInlineError(QuickKeysError, duplicated ? "重複するキーは保存されません。" : string.Empty);
        _quickKeys.Save(values);
    }

    private void OnResetQuickKeys(object sender, RoutedEventArgs args)
    {
        _quickKeys.Save(QuickKeyRegistry.DefaultMappings);
        LoadQuickKeys();
        SetInlineError(QuickKeysError, string.Empty);
    }


    private void OnStartupToggled(object sender, RoutedEventArgs args)
    {
        if (_loading) return;
        try
        {
            StartupRegistration.SetEnabled(StartupToggle.IsOn, Environment.ProcessPath!);
            R1Storage.WriteLog($"INFO Startup registration enabled={StartupToggle.IsOn}");
        }
        catch (Exception exception) { R1Storage.WriteLog($"ERROR Startup registration failed: {exception.Message}"); }
    }

    private void OnAppearanceChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_loading || AppearanceBox.SelectedValue is not string appearance) return;
        R1Storage.Set("Appearance", appearance);
        ApplyAppearance(appearance);
        _applyAppearance(appearance);
    }

    private void ApplyAppearance(string appearance)
    {
        SettingsRoot.RequestedTheme = appearance switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }

    private async Task RefreshEverythingStatusAsync()
    {
        EverythingStatusText.Text = "状態を確認しています…";
        var availability = await Beacon.Platform.Windows.Everything.EverythingApi.GetAvailabilityAsync(CancellationToken.None);
        EverythingStatusText.Text = availability.Available ? "接続済み" : "未接続 — Windows Indexで検索します";
    }

    private void OnRecheckEverything(object sender, RoutedEventArgs args) => _ = RefreshEverythingStatusAsync();

    private async void OnPickExcludedExecutable(object sender, RoutedEventArgs args)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.FileTypeFilter.Add(".exe");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            AddExcludedAppRow(file.Path);
            SaveExcludedApps();
        }
        catch (Exception exception) { R1Storage.WriteLog($"ERROR Excluded application picker failed: {exception.Message}"); }
    }

    private async void OnPickRunningApp(object sender, RoutedEventArgs args)
    {
        var names = Process.GetProcesses()
            .Select(process => { try { return process.ProcessName; } catch { return null; } })
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var picker = new ComboBox { ItemsSource = names, PlaceholderText = "起動中のアプリ" };
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "除外するアプリを選択",
            Content = picker,
            PrimaryButtonText = "追加",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || picker.SelectedItem is not string selected) return;
        AddExcludedAppRow(selected);
        SaveExcludedApps();
    }
    private void OnClipboardToggled(object sender, RoutedEventArgs args) { if (!_loading && ClipboardToggle.IsOn != _clipboardEnabled()) _toggleClipboard(); }
    private void OnPersonalizationToggled(object sender, RoutedEventArgs args) { if (!_loading && PersonalizationToggle.IsOn != _personalizationEnabled()) _togglePersonalization(); }

    private async Task<bool> ConfirmDeleteAsync(string title)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            FontFamily = (FontFamily)Application.Current.Resources["LauncherFontFamily"],
            Title = title,
            Content = "この操作は取り消せません。",
            PrimaryButtonText = "削除",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Close,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async void OnResetPersonalization(object sender, RoutedEventArgs args)
    {
        if (await ConfirmDeleteAsync("個人化データをすべて削除しますか？")) _resetPersonalization();
    }

    private async void OnClearClipboard(object sender, RoutedEventArgs args)
    {
        if (await ConfirmDeleteAsync("クリップボード履歴をすべて削除しますか？")) _clearClipboard();
    }

    private void LoadExcludedApps()
    {
        var wasLoading = _loading;
        _loading = true;
        ExcludedAppsRows.Children.Clear();
        foreach (var value in R1Storage.Get("ClipboardExcludedApplications", Array.Empty<string>())) AddExcludedAppRow(value);
        _loading = wasLoading;
    }

    private void AddExcludedAppRow(string value = "")
    {
        var box = new TextBox
        {
            PlaceholderText = "プロセス名または実行ファイル名",
            Text = value,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(box, "除外するアプリ");
        var remove = CreateRemoveButton();
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(remove, 1);
        grid.Children.Add(box);
        grid.Children.Add(remove);
        var card = CreateRowCard(grid);
        box.LostFocus += (_, _) => SaveExcludedApps();
        remove.Click += (_, _) => { ExcludedAppsRows.Children.Remove(card); SaveExcludedApps(); };
        ExcludedAppsRows.Children.Add(card);
    }

    private void OnAddExcludedApp(object sender, RoutedEventArgs args) => AddExcludedAppRow();

    private void SaveExcludedApps()
    {
        if (_loading) return;
        var values = ExcludedAppsRows.Children.OfType<Border>()
            .Select(border => ((Grid)border.Child).Children.OfType<TextBox>().Single().Text.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        R1Storage.Set("ClipboardExcludedApplications", values);
        _applyClipboardExclusions(values);
    }

    private void OnSettingsNavigationChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = (args.SelectedItemContainer?.Tag as string) ?? "general";
        PageTitle.Text = (args.SelectedItemContainer?.Content as string) ?? "一般";
        GeneralSection.Visibility = tag == "general" ? Visibility.Visible : Visibility.Collapsed;
        SearchSection.Visibility = tag == "search" ? Visibility.Visible : Visibility.Collapsed;
        QuickKeysSection.Visibility = tag == "quickkeys" ? Visibility.Visible : Visibility.Collapsed;
        PrivacySection.Visibility = tag == "privacy" ? Visibility.Visible : Visibility.Collapsed;
        AboutSection.Visibility = tag == "about" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnOpenAttribution(object sender, RoutedEventArgs args) => OpenDistributionFile("attribution.md");
    private void OnOpenLicense(object sender, RoutedEventArgs args) => OpenDistributionFile("LICENSE");

    public void Dispose()
    {
        SystemBackdrop = null;
        GC.SuppressFinalize(this);
    }

    private static void OpenDistributionFile(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, name);
        if (File.Exists(path)) ProcessLaunchService.Start(path);
    }
}
