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

namespace Beacon.WinUI;

public sealed partial class MainWindow
{
    private readonly AppSearchProvider _appSearchProvider;
    private readonly QueryOrchestrator _orchestrator;
    private readonly ObservableCollection<ResultRow> _results = [];
    private readonly IconResolver _icons = new();
    private bool _composing;
    private string? _activeProcessName;
    private string? _activeFolder;
    private nint _windowHandle;


    private static double Token(string key) => (double)Application.Current.Resources[key];
    private static int Pixels(double dip, uint dpi) => (int)Math.Round(dip * dpi / 96d);

    /// <summary>GridのAuto行が実際に占める高さ（要素の高さ＋自分の上下Margin）。</summary>
    private static double BlockHeight(string heightKey, string marginKey)
    {
        var margin = (Thickness)Application.Current.Resources[marginKey];
        return Token(heightKey) + margin.Top + margin.Bottom;
    }

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
        ApplyPendingAppearance();
        _everythingStatusShown = false;
        _activeProcessName = ActiveWindowService.GetProcessName();
        _viewState.Reset();
        QueryPlaceholder.Text = "Anything...";
        RestoreScopeChip();
        ClearLauncherStatus(resize: false);
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
        _activeFolder = null;
        _ = RefreshActiveFolderAsync();
        _ = RefreshAppCacheAsync();
        _bookmarkProvider.Preload();
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

    private async Task RefreshActiveFolderAsync()
    {
        try { _activeFolder = await Task.Run(ExplorerPathService.GetCurrentPath); }
        catch (Exception exception) { R1Storage.WriteLog($"ERROR Explorer path refresh failed: {exception.Message}"); }
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
        if (_viewState.State == LauncherViewState.ActionInput) return;
        StartSearch(QueryBox.Text);
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
            ClearLauncherStatus(resize: false);
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
        TryQuickKey(query, out var searchText, out _);
        // 「検索中…」の表示可否はSearchAsync/Flush側（M1+M2）が結果の有無を見て判断する。
        // ここで無条件に出すと、結果が既に表示されている間も高さを予約し続けてしまう。
        _ = SearchAsync(searchText, query);
    }

    /// <summary>
    /// stable-prefix / append-only 方式: 一度表示した上位行の相対順序を凍結（committed）し、
    /// 遅延到着の候補は凍結プレフィックスの後ろの空きへスコア順に追記するだけで、既存の表示行を
    /// 並び替えない（reshuffle回避、実マージロジックは <see cref="ResultMerger"/> に純関数として抽出）。
    /// 32ms周期のタイマーで短い収集窓ごとに再描画し、初回paintの即時性（アプリ/電卓/URL/Web）と
    /// ファイル検索完了までの安定を両立する。
    /// </summary>
    /// <remarks>
    /// スレッド前提: このメソッド内の <c>Flush</c>／<c>candidates</c>／<c>committed</c> は一切ロックしない。
    /// <c>DispatcherQueueTimer.Tick</c> と <c>await foreach</c> の再開はいずれもUIスレッドの
    /// <c>DispatcherQueue</c> コンテキスト上で単一スレッド実行されるため安全（<see cref="QueryOrchestrator"/>
    /// が内部で <c>ConfigureAwait(false)</c> を使わないことに依存する）。この前提を崩す変更（別スレッドでの
    /// await継続やConfigureAwait(false)の追加）をする場合はロックを追加すること。
    /// </remarks>
    private async Task SearchAsync(string query, string displayedQuery)
    {
        var started = Stopwatch.GetTimestamp();
        var maximumCandidateCount = (int)Application.Current.Resources["MaximumCandidateCount"];
        var scope = _queryScope?.ProviderScope ?? (_viewState.BrowseCategory switch
        {
            BrowseCategory.Applications => QueryScope.Applications,
            BrowseCategory.Files => QueryScope.Files,
            _ => QueryScope.All,
        });
        var context = new RankingContext(DateTimeOffset.Now, _activeProcessName, _activeFolder, _usageHistory.Enabled);

        var candidates = new Dictionary<string, SearchResultDto>(StringComparer.Ordinal);
        var committed = new List<string>();
        var firstCandidate = true;
        var firstPaintDone = false;
        var stableLogged = false;
        var lastArrival = started;
        var resizeRequests = 0;

        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(32);

        void Flush(bool final)
        {
            if (!string.Equals(displayedQuery, QueryBox.Text, StringComparison.Ordinal)) return;
            var display = ResultMerger.Merge(committed, candidates, maximumCandidateCount).ToArray();
            committed = display.Select(r => r.Id).ToList();
            // M1+M2: 検索中に1件以上表示されている間は「検索中…」で高さを予約しない・読み上げない。
            // まだ0件の間だけ過渡ステータスを出す。終端ステータス（final後の分岐）はここでは触らない。
            if (!final)
            {
                if (display.Length == 0) SetLauncherStatus("検索中…", resize: false);
                else ClearLauncherStatus(resize: false);
            }
            ApplyResults(display);
            ResizeForResults(display.Length);
            resizeRequests++;
            if (!firstPaintDone)
            {
                firstPaintDone = true;
                R1Storage.WriteLog($"PERF InputToFirstPaintMs={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1}");
            }
            if (final && !stableLogged)
            {
                stableLogged = true;
                R1Storage.WriteLog($"PERF InputToStableResultsMs={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1}");
            }
        }

        timer.Tick += (_, __) =>
        {
            Flush(final: false);
            if (firstPaintDone && !stableLogged &&
                Stopwatch.GetElapsedTime(lastArrival).TotalMilliseconds >= 96)
            {
                stableLogged = true;
                R1Storage.WriteLog($"PERF InputToStableResultsMs={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1}");
            }
        };

        try
        {
            if (_queryScope?.IsClipboard == true)
            {
                // H1: この経路は通常のFlushを一切通らないため、直前のクエリ由来の過渡/終端ステータスが
                // クリップボード結果の上に残留しないよう明示的にクリアする。
                ClearLauncherStatus(resize: false);
                ApplyClipboardResults();
                return;
            }
            timer.Start();
            await foreach (var candidate in _orchestrator.SearchAsync(query, scope, rankingContext: context))
            {
                if (!string.Equals(displayedQuery, QueryBox.Text, StringComparison.Ordinal)) return;
                if (firstCandidate)
                {
                    firstCandidate = false;
                    R1Storage.WriteLog($"PERF InputToFirstCandidateMs={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1}");
                }
                candidates[candidate.Id] = candidate;
                lastArrival = Stopwatch.GetTimestamp();
            }
            Flush(final: true);
            if (!string.Equals(displayedQuery, QueryBox.Text, StringComparison.Ordinal)) return;
            var unavailableCount = _orchestrator.CurrentSession?.UnresponsiveProviderCount ?? 0;
            if (unavailableCount > 0)
                SetLauncherStatus("一部の検索元が応答しません。", retry: true);
            else if (_everythingAvailable == false && scope is QueryScope.All or QueryScope.Files)
                ShowEverythingUnavailableStatus();
            else if (_results.Count == 0)
                SetLauncherStatus("結果が見つかりませんでした。", retry: true);
            else
                ClearLauncherStatus();
        }
        catch (OperationCanceledException)
        {
            if (string.Equals(displayedQuery, QueryBox.Text, StringComparison.Ordinal))
                SetLauncherStatus("検索をキャンセルしました。", retry: true);
        }
        catch (Exception exception)
        {
            R1Storage.WriteLog($"ERROR Search failed: {exception.Message}");
            if (string.Equals(displayedQuery, QueryBox.Text, StringComparison.Ordinal))
                SetLauncherStatus("検索中に問題が発生しました。", retry: true);
        }
        finally
        {
            timer.Stop();
            R1Storage.WriteLog($"PERF QueryResizeRequests={resizeRequests} QueryLength={displayedQuery.Length}");
        }
    }
    private void ApplyResults(SearchResultDto[] visible, bool resolveIcons = true)
    {
        var changed = false;
        TryQuickKey(QueryBox.Text, out var matchQuery, out _);
        for (var index = 0; index < visible.Length; index++)
        {
            if (index < _results.Count && _results[index].Result.Id == visible[index].Id)
            {
                if (_results[index].Update(visible[index], matchQuery))
                {
                    changed = true;
                    if (resolveIcons) _ = ResolveIconAsync(_results[index]);
                }
                continue;
            }
            var existing = -1;
            for (var candidate = index + 1; candidate < _results.Count; candidate++)
                if (_results[candidate].Result.Id == visible[index].Id) { existing = candidate; break; }
            if (existing >= 0)
            {
                _results.Move(existing, index);
                if (_results[index].Update(visible[index], matchQuery) && resolveIcons) _ = ResolveIconAsync(_results[index]);
            }
            else
            {
                var row = new ResultRow(visible[index], matchQuery);
                _results.Insert(index, row);
                if (resolveIcons) _ = ResolveIconAsync(row);
            }
            changed = true;
        }
        while (_results.Count > visible.Length) { _results.RemoveAt(_results.Count - 1); changed = true; }
        if (changed)
        {
            ResultsList.SelectedIndex = _results.Count == 0 ? -1 : 0;
            if (_results.Count > 0) ResultsList.ScrollIntoView(_results[0]);
            UpdateQuickKeyBadges();
        }
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
            SetLauncherStatus("実行できませんでした。", retry: true);
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
        catch (Exception exception)
        {
            R1Storage.WriteLog($"ERROR Execute failed: {exception.Message}");
            SetLauncherStatus("実行できませんでした。", retryAction: () => Execute(result));
        }
    }

    private void ResizeForResults(int visibleCount)
    {
        var statusVisible = StatusRow.Visibility == Visibility.Visible;
        var expanded = visibleCount > 0 || statusVisible;
        Panel.CornerRadius = (CornerRadius)Application.Current.Resources[expanded ? "ExpandedPanelCornerRadius" : "SearchBarCornerRadius"];
        ResultsList.Visibility = visibleCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        var dpi = NativeMethods.GetDpiForWindow(_windowHandle);
        var width = Pixels(Token("LauncherWidth"), dpi);
        var targetDip = LauncherHeight.Calculate(
            Token("SearchBarHeight"),
            Token("ResultRowHeight"),
            Token("ResultsListVerticalSpace"),
            visibleCount,
            (int)Application.Current.Resources["MaximumResultCount"],
            scopeChipBlockHeight: ScopeChip.Visibility == Visibility.Visible
                ? BlockHeight("CategoryChipHeight", "CategoryChipRowMargin")
                : 0,
            statusRowBlockHeight: statusVisible
                ? BlockHeight("StatusRowMinHeight", "StatusRowMargin")
                : 0);
        AnimateResize(width, Pixels(targetDip, dpi));
    }


    private void AnimateResize(int width, int target)
    {
        var start = _appWindow.Size.Height;
        if (start == target) return;
        ApplyWindowRegion(width, target, NativeMethods.GetDpiForWindow(_windowHandle));
        ResizeWindow(width, target);
        R1Storage.WriteLog($"PERF Resize direction={(target > start ? "expand" : "collapse")} AppWindowResizeCalls=1 SetWindowRgnCalls=1 Frames=0 DroppedFrames=0 MaxFrameMs=0.0");
    }

    private void ResizeWindow(int width, int height)
    {
        _appWindow.Resize(new SizeInt32(width, height));
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
    public ResultRow(SearchResultDto result, string matchQuery)
    {
        Result = result;
        MatchQuery = matchQuery;
        Glyph = GlyphFor(result);
    }
    public SearchResultDto Result { get; private set; }
    public string MatchQuery { get; private set; }
    public string Glyph { get; private set; }
    public Microsoft.UI.Xaml.Media.ImageSource? Image { get; private set; }
    public Visibility GlyphVisibility => Image is null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ImageVisibility => Image is null ? Visibility.Collapsed : Visibility.Visible;
    public string? QuickKey { get; private set; }
    public Visibility QuickKeyVisibility => QuickKey is null ? Visibility.Collapsed : Visibility.Visible;
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public bool Update(SearchResultDto result, string matchQuery)
    {
        var resultChanged = !ReferenceEquals(Result, result);
        var queryChanged = !string.Equals(MatchQuery, matchQuery, StringComparison.Ordinal);
        if (!resultChanged && !queryChanged) return false;
        var iconChanged = Result.Icon != result.Icon;
        Result = result;
        MatchQuery = matchQuery;
        if (resultChanged) PropertyChanged?.Invoke(this, new(nameof(Result)));
        if (queryChanged) PropertyChanged?.Invoke(this, new(nameof(MatchQuery)));
        if (iconChanged)
        {
            Image = null;
            Glyph = GlyphFor(result);
            NotifyIconChanged();
        }
        return true;
    }

    public void SetQuickKey(string? quickKey)
    {
        if (string.Equals(QuickKey, quickKey, StringComparison.OrdinalIgnoreCase)) return;
        QuickKey = quickKey;
        PropertyChanged?.Invoke(this, new(nameof(QuickKey)));
        PropertyChanged?.Invoke(this, new(nameof(QuickKeyVisibility)));
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
