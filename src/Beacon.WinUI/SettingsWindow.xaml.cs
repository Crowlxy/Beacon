using System.Reflection;
using Beacon.Contracts;
using Beacon.Core;
using Beacon.Platform.Windows;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Beacon.WinUI;

public sealed partial class SettingsWindow : Window
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
    private bool _loading = true;

    public SettingsWindow(
        string dataRoot,
        ChangeHotkey changeHotkey,
        Func<bool> clipboardEnabled,
        Action toggleClipboard,
        Func<bool> personalizationEnabled,
        Action togglePersonalization,
        Action resetPersonalization,
        Action clearClipboard,
        Action<string[]> applyClipboardExclusions)
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
        InitializeComponent();
        try { SystemBackdrop = new MicaBackdrop(); }
        catch (Exception exception)
        {
            SettingsNavigation.Background = (Brush)Application.Current.Resources["LauncherFallbackBrush"];
            R1Storage.WriteLog($"WARN Settings backdrop unavailable: {exception.Message}");
        }
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "App.ico");
        if (File.Exists(iconPath)) AppWindow.SetIcon(iconPath);
        AppWindow.Closing += (_, args) =>
        {
            args.Cancel = true;
            SaveQuickKeys();
            SaveExcludedApps();
            AppWindow.Hide();
        };
        Activated += (_, _) => RefreshToggleState();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(
            (int)(double)Application.Current.Resources["SettingsWindowWidth"],
            (int)(double)Application.Current.Resources["SettingsWindowHeight"]));
        HotkeyBox.Text = R1Storage.Get("GlobalHotkey", "Alt+Shift+Space");
        LoadQuickKeys();
        ClipboardToggle.IsOn = _clipboardEnabled();
        PersonalizationToggle.IsOn = _personalizationEnabled();
        LoadExcludedApps();
        SettingsNavigation.SelectedItem = SettingsNavigation.MenuItems[0];
        VersionText.Text = $"Version {Assembly.GetExecutingAssembly().GetName().Version}";
        ContractText.Text = $"ContractVersion {ContractVersion.Current}";
        _loading = false;
    }

    private void RefreshToggleState()
    {
        _loading = true;
        ClipboardToggle.IsOn = _clipboardEnabled();
        PersonalizationToggle.IsOn = _personalizationEnabled();
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
        foreach (var value in R1Storage.Get("QuickKeys", DefaultQuickKeys())) AddQuickKeyRow(value.Key, value.Value);
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
        R1Storage.Set("QuickKeys", values);
    }

    private void OnResetQuickKeys(object sender, RoutedEventArgs args)
    {
        R1Storage.Set("QuickKeys", DefaultQuickKeys());
        LoadQuickKeys();
        SetInlineError(QuickKeysError, string.Empty);
    }

    private static Dictionary<string, string> DefaultQuickKeys() => new() { ["rf"] = "reveal", ["cp"] = "copy-path", ["rn"] = "rename", ["term"] = "terminal" };
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

    private void OnResetEverythingNotice(object sender, RoutedEventArgs args) => R1Storage.SetBoolean("EverythingNoticeShown", false);
    private void OnOpenAttribution(object sender, RoutedEventArgs args) => OpenDistributionFile("attribution.md");
    private void OnOpenLicense(object sender, RoutedEventArgs args) => OpenDistributionFile("LICENSE");
    private static void OpenDistributionFile(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, name);
        if (File.Exists(path)) ProcessLaunchService.Start(path);
    }
}
