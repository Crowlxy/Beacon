using System.Collections.ObjectModel;
using System.Diagnostics;
using Beacon.Contracts;
using Beacon.Core;
using Beacon.Platform.Windows;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;
using Windows.UI.ViewManagement;

namespace Beacon.WinUI;

public sealed partial class MainWindow
{
    private readonly AppSearchProvider _appSearchProvider;
    private readonly QueryOrchestrator _orchestrator;
    private readonly ObservableCollection<ResultRow> _results = [];
    private readonly IconResolver _icons = new();
    private readonly UISettings _uiSettings = new();
    private bool _composing;
    private string? _activeProcessName;
    private string? _activeFolder;
    private nint _windowHandle;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _resizeTimer;

    private static double Token(string key) => (double)Application.Current.Resources[key];
    private static int Pixels(double dip, uint dpi) => (int)Math.Round(dip * dpi / 96d);

    private void InitializeLauncher(nint windowHandle)
    {
        _windowHandle = windowHandle;
        ResultsList.ItemsSource = _results;
        Activated += OnWindowActivated;
        ExtendsContentIntoTitleBar = true;
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
        }
        if (!NativeMethods.ApplyWindowFrameStyle(windowHandle)) R1Storage.WriteLog("ERROR DWM rounded corners or border suppression failed");
        ResizeForResults(0);
    }

    private void ShowLauncherCore()
    {
        var started = Stopwatch.GetTimestamp();
        _activeProcessName = ActiveWindowService.GetProcessName();
        _activeFolder = ExplorerPathService.GetCurrentPath();
        _ = RefreshAppCacheAsync();
        _viewState.Reset();
        QueryBox.Text = string.Empty;
        ApplyResults([]);
        ResizeForResults(0);
        RepositionLauncher();
        if (SystemBackdrop is ThinDesktopAcrylicBackdrop acrylic) acrylic.SetInputActive(true);
        NativeMethods.ShowWindow(_windowHandle, NativeMethods.ShowWindowCommand.Show);
        Activate();
        if (!NativeMethods.BringToForeground(_windowHandle))
            R1Storage.WriteLog("WARN Foreground activation was limited by Windows");
        DispatcherQueue.TryEnqueue(() => QueryBox.Focus(FocusState.Programmatic));
        R1Storage.WriteLog($"PERF HotkeyToDisplayMs={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1}");
        R1Storage.WriteLog("INFO Hotkey or activation pipe displayed the AppWindow");
    }

    private void RepositionLauncher()
    {
        NativeMethods.GetCursorPos(out var cursor);
        var display = DisplayArea.GetFromPoint(new PointInt32(cursor.X, cursor.Y), DisplayAreaFallback.Nearest);
        var dpi = MonitorDpi(cursor);
        var width = Pixels(Token("LauncherWidth"), dpi);
        var height = Pixels(Token("SearchBarHeight"), dpi);
        _appWindow.MoveAndResize(new RectInt32(
            display.WorkArea.X + ((display.WorkArea.Width - width) / 2),
            display.WorkArea.Y + (int)(display.WorkArea.Height * Token("LauncherTopRatio")),
            width,
            height));
        ApplyWindowRegion(width, height, dpi);
    }

    private async Task RefreshAppCacheAsync()
    {
        try { await _appSearchProvider.RefreshIfWatcherUnavailableAsync(); }
        catch (Exception exception) { R1Storage.WriteLog($"ERROR App cache refresh failed: {exception.Message}"); }
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (SystemBackdrop is ThinDesktopAcrylicBackdrop acrylic)
            acrylic.SetInputActive(args.WindowActivationState != WindowActivationState.Deactivated);
        if (args.WindowActivationState == WindowActivationState.Deactivated) _appWindow.Hide();
        else DispatcherQueue.TryEnqueue(() => QueryBox.Focus(FocusState.Programmatic));
    }

    private void OnCompositionStarted(UIElement sender, TextCompositionStartedEventArgs args) => _composing = true;
    private void OnCompositionEnded(UIElement sender, TextCompositionEndedEventArgs args) => _composing = false;

    private void OnQueryTextChanged(object sender, TextChangedEventArgs args)
    {
        QueryPlaceholder.Visibility = QueryBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateGhostCompletion(QueryBox.Text);
        if (!_composing && _viewState.State != LauncherViewState.ActionInput) StartSearch(QueryBox.Text);
    }

    private void StartSearch(string query)
    {
        if (_viewState.State == LauncherViewState.Browse && _viewState.BrowseCategory == BrowseCategory.Actions)
        {
            ApplyActionResults(query);
            return;
        }
        if (_viewState.State == LauncherViewState.Browse && _viewState.BrowseCategory == BrowseCategory.Clipboard)
        {
            ApplyClipboardResults(query);
            return;
        }
        if (string.IsNullOrWhiteSpace(query))
        {
            _orchestrator.Cancel();
            if (_viewState.State == LauncherViewState.Browse && _viewState.BrowseCategory == BrowseCategory.Applications)
                _ = ApplyApplicationBrowseAsync();
            else if (_viewState.State == LauncherViewState.Browse && _viewState.BrowseCategory == BrowseCategory.Files)
            {
                var recent = WindowsRecentFiles.Get().ToArray();
                ApplyResults(recent);
                ResizeForResults(recent.Length);
            }
            else
            {
                ApplyResults([]);
                ResizeForResults(0);
            }
            return;
        }
        ApplyResults([]);
        ResizeForResults(0);
        TryQuickKey(query, out var searchText, out _);
        _ = SearchAsync(searchText, query);
    }

    private async Task SearchAsync(string query, string displayedQuery)
    {
        var started = Stopwatch.GetTimestamp();
        var firstResult = true;
        try
        {
            var pending = new List<SearchResultDto>();
            if (_queryScope?.IsClipboard == true) { ApplyClipboardResults(); return; }
            var scope = _queryScope?.ProviderScope ?? (_viewState.BrowseCategory switch
            {
                BrowseCategory.Applications => QueryScope.Applications,
                BrowseCategory.Files => QueryScope.Files,
                _ => QueryScope.All,
            });
            await foreach (var result in _orchestrator.SearchAsync(
                               query,
                               scope,
                               rankingContext: new RankingContext(DateTimeOffset.Now, _activeProcessName, _activeFolder, _usageHistory.Enabled)))
            {
                if (!string.Equals(displayedQuery, QueryBox.Text, StringComparison.Ordinal)) return;
                if (firstResult)
                {
                    firstResult = false;
                    R1Storage.WriteLog($"PERF InputToFirstResultMs={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1}");
                }
                pending.RemoveAll(item => item.Id == result.Id);
                pending.Add(result);
                var visible = pending.OrderByDescending(item => item.Score)
                    .Take((int)Application.Current.Resources["MaximumResultCount"]).ToArray();
                ApplyResults(visible);
                ResizeForResults(visible.Length);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { R1Storage.WriteLog($"ERROR Search failed: {exception.Message}"); }
    }

    private void ApplyResults(SearchResultDto[] visible)
    {
        var changed = false;
        for (var index = 0; index < visible.Length; index++)
        {
            if (index < _results.Count && _results[index].Result.Id == visible[index].Id)
            {
                if (_results[index].Update(visible[index]))
                {
                    changed = true;
                    _ = ResolveIconAsync(_results[index]);
                }
                continue;
            }
            var existing = -1;
            for (var candidate = index + 1; candidate < _results.Count; candidate++)
                if (_results[candidate].Result.Id == visible[index].Id) { existing = candidate; break; }
            if (existing >= 0)
            {
                _results.Move(existing, index);
                if (_results[index].Update(visible[index])) _ = ResolveIconAsync(_results[index]);
            }
            else
            {
                var row = new ResultRow(visible[index]);
                _results.Insert(index, row);
                _ = ResolveIconAsync(row);
            }
            changed = true;
        }
        while (_results.Count > visible.Length) { _results.RemoveAt(_results.Count - 1); changed = true; }
        if (changed) ResultsList.SelectedIndex = _results.Count == 0 ? -1 : 0;
    }
    private async Task ResolveIconAsync(ResultRow row)
    {
        var image = await _icons.ResolveAsync(row.Result.Icon);
        if (!_results.Contains(row) || image is null) return;
        row.SetImage(image);
    }

    private void OnQueryKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (HandleUxKey(args)) args.Handled = true;
        else if (args.Key is VirtualKey.Up or VirtualKey.Down)
        {
            if (_results.Count == 0) return;
            var delta = args.Key == VirtualKey.Up ? -1 : 1;
            ResultsList.SelectedIndex = Math.Clamp(ResultsList.SelectedIndex + delta, 0, _results.Count - 1);
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            args.Handled = true;
        }
        else if (args.Key == VirtualKey.Enter && !_composing && ResultsList.SelectedItem is ResultRow row)
        {
            HandleEnter(row);
            args.Handled = true;
        }
    }

    private void OnResultClicked(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is ResultRow row) HandleEnter(row);
    }

    private void Execute(SearchResultDto result)
    {
        var session = _orchestrator.CurrentSession;
        if (session is null || string.IsNullOrWhiteSpace(result.ExecutionToken)) return;
        var request = new ExecuteRequest(session.SessionId, result.Id, result.ExecutionToken, ContractVersion.Current);
        var validation = _orchestrator.ValidateExecuteRequest(request);
        if (!validation.Success)
        {
            R1Storage.WriteLog($"ERROR Execute rejected: {validation.FailureReason}");
            return;
        }
        try
        {
            if (result.ProviderId == SystemActionSearchProvider.Id)
            {
                if (SystemActionService.RequiresConfirmation(result.ExecutionToken) && !_systemActionConfirmed) return;
                _systemActionConfirmed = false;
                SystemActionService.Execute(result.ExecutionToken);
            }
            else if (result.ProviderId == ShellSearchProvider.Id) ShellExecutionService.Run(result.ExecutionToken, _activeFolder);
            else if (result.ProviderId == ProcessKillerSearchProvider.Id) ProcessTerminationService.Terminate(result.ExecutionToken);
            else if (result.Kind == ResultKind.Calculation) ClipboardTextService.Set(result.CopyText ?? result.ExecutionToken);
            else if (result.Kind == ResultKind.Folder) FileOperationService.Open(result.FilePath ?? result.ExecutionToken);
            else ProcessLaunchService.Start(result.FilePath ?? result.ExecutionToken);
            _usageHistory.Record(result.Id, _activeProcessName, "Search");
            _appWindow.Hide();
        }
        catch (Exception exception) { R1Storage.WriteLog($"ERROR Execute failed: {exception.Message}"); }
    }

    private void ResizeForResults(int visibleCount)
    {
        var expanded = visibleCount > 0;
        Panel.CornerRadius = (CornerRadius)Application.Current.Resources[expanded ? "ExpandedPanelCornerRadius" : "SearchBarCornerRadius"];
        ResultsList.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        var dpi = NativeMethods.GetDpiForWindow(_windowHandle);
        var width = Pixels(Token("LauncherWidth"), dpi);
        var targetDip = LauncherHeight.Calculate(Token("SearchBarHeight"), Token("ResultRowHeight"),
            Token("ResultsListVerticalSpace"), visibleCount, (int)Application.Current.Resources["MaximumResultCount"]);
        if (ScopeChip.Visibility == Visibility.Visible) targetDip += Token("CategoryChipHeight");
        AnimateResize(width, Pixels(targetDip, dpi));
    }

    private void AnimateResize(int width, int target)
    {
        var start = _appWindow.Size.Height;
        _resizeTimer?.Stop();
        _resizeTimer = null;
        if (!_uiSettings.AnimationsEnabled || start == target || start == 0)
        {
            ResizeWindow(width, target);
            return;
        }
        var watch = Stopwatch.StartNew();
        var duration = Token("PanelAnimationMilliseconds");
        var decay = Token("PanelAnimationDecay");
        _resizeTimer = DispatcherQueue.CreateTimer();
        _resizeTimer.Interval = TimeSpan.FromMilliseconds(Token("AnimationFrameMilliseconds"));
        _resizeTimer.Tick += (_, _) =>
        {
            var progress = Math.Min(1d, watch.Elapsed.TotalMilliseconds / duration);
            var eased = progress == 1d ? 1d : 1d - Math.Exp(-decay * progress);
            ResizeWindow(width, (int)Math.Round(start + ((target - start) * eased)));
            if (progress < 1d) return;
            _resizeTimer.Stop();
            _resizeTimer = null;
        };
        _resizeTimer.Start();
    }

    private void ResizeWindow(int width, int height)
    {
        _appWindow.Resize(new SizeInt32(width, height));
        ApplyWindowRegion(width, height, NativeMethods.GetDpiForWindow(_windowHandle));
    }

    private void ApplyWindowRegion(int width, int height, uint dpi)
    {
        var radius = Pixels(Panel.CornerRadius.TopLeft, dpi);
        if (!NativeMethods.ApplyRoundedRegion(_windowHandle, width, height, radius))
            R1Storage.WriteLog("ERROR Rounded window region failed");
    }

    private static uint MonitorDpi(NativeMethods.Point point)
    {
        var monitor = NativeMethods.MonitorFromPoint(point, 2);
        return NativeMethods.GetDpiForMonitor(monitor, 0, out var x, out _) == 0 ? x : 96;
    }
}

public sealed class ResultRow : System.ComponentModel.INotifyPropertyChanged
{
    public ResultRow(SearchResultDto result)
    {
        Result = result;
        Glyph = GlyphFor(result);
    }
    public SearchResultDto Result { get; private set; }
    public string Glyph { get; private set; }
    public Microsoft.UI.Xaml.Media.ImageSource? Image { get; private set; }
    public Visibility GlyphVisibility => Image is null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ImageVisibility => Image is null ? Visibility.Collapsed : Visibility.Visible;
    public string? QuickKey => !string.IsNullOrWhiteSpace(Result.FilePath) ? (Result.Kind == ResultKind.Folder ? "term" : "rf") : null;
    public Visibility QuickKeyVisibility => QuickKey is null ? Visibility.Collapsed : Visibility.Visible;
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public bool Update(SearchResultDto result)
    {
        if (ReferenceEquals(Result, result)) return false;
        var iconChanged = Result.Icon != result.Icon;
        Result = result;
        PropertyChanged?.Invoke(this, new(nameof(Result)));
        PropertyChanged?.Invoke(this, new(nameof(QuickKey)));
        PropertyChanged?.Invoke(this, new(nameof(QuickKeyVisibility)));
        if (iconChanged)
        {
            Image = null;
            Glyph = GlyphFor(result);
            NotifyIconChanged();
        }
        return true;
    }

    public void SetImage(Microsoft.UI.Xaml.Media.ImageSource image)
    {
        Image = image;
        NotifyIconChanged();
    }

    private static string GlyphFor(SearchResultDto result) =>
        result.Icon.Source == Beacon.Contracts.IconSource.FluentGlyph && !string.IsNullOrEmpty(result.Icon.Value) ? result.Icon.Value : "\uE7C3";

    private void NotifyIconChanged()
    {
        PropertyChanged?.Invoke(this, new(nameof(Image)));
        PropertyChanged?.Invoke(this, new(nameof(Glyph)));
        PropertyChanged?.Invoke(this, new(nameof(GlyphVisibility)));
        PropertyChanged?.Invoke(this, new(nameof(ImageVisibility)));
    }
}
