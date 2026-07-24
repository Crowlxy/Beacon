using Beacon.Contracts;
using Beacon.Core;
using Beacon.Platform.Windows;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using IconSource = Beacon.Contracts.IconSource;

namespace Beacon.WinUI;

public sealed partial class MainWindow
{
    private const string ActionProviderId = "beacon.actions";
    private const string StatusProviderId = "beacon.status";
    private readonly LauncherStateMachine _viewState = new();
    private QueryScopeSelection? _queryScope;
    private SearchResultDto? _actionTarget;
    private ActionDescriptor? _selectedAction;
    private ActionInputFlow? _actionInputFlow;
    private string? _actionArgument;
    private SearchResultDto? _pendingSystemResult;
    private bool _systemActionConfirmed;
    private bool _clearClipboardPending;
    private CancellationTokenSource? _runningCancellation;
    private readonly QuickKeyRegistry _quickKeys = CreateQuickKeyRegistry();
    private Action? _statusRetry;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _statusAutoHideTimer;
    private bool _everythingStatusShown;

    private bool HandleUxKey(KeyRoutedEventArgs args)
    {
        if (NativeMethods.ControlPressed() && args.Key is VirtualKey.Number1 or VirtualKey.Number2 or VirtualKey.Number3 or VirtualKey.Number4)
        {
            EnterBrowse((BrowseCategory)((int)args.Key - (int)VirtualKey.Number1));
            return true;
        }
        if (args.Key == VirtualKey.Tab && QueryScopeSelection.TryParse(QueryBox.Text, out var scope))
        {
            SetScope(scope);
            return true;
        }
        if (args.Key == VirtualKey.Back && QueryBox.Text.Length == 0 && _queryScope is not null)
        {
            ClearScope();
            return true;
        }
        if (args.Key == VirtualKey.Escape)
        {
            var leaving = _viewState.State;
            if (leaving == LauncherViewState.Running) _runningCancellation?.Cancel();
            if (leaving == LauncherViewState.Confirmation)
            {
                _clearClipboardPending = false;
                _pendingSystemResult = null;
            }
            switch (_viewState.Back(QueryBox.Text.Length > 0))
            {
                case BackOutcome.Close: _appWindow.Hide(); break;
                case BackOutcome.ClearQuery: QueryBox.Text = string.Empty; break;
                case BackOutcome.StateChanged: RestoreState(); break;
            }
            return true;
        }
        if ((args.Key == VirtualKey.Right || (args.Key == VirtualKey.Enter && NativeMethods.ShiftPressed())) && ResultsList.SelectedItem is ResultRow contextRow)
        {
            OpenActions(contextRow.Result);
            return true;
        }
        if (args.Key == VirtualKey.Delete && _viewState.BrowseCategory == BrowseCategory.Clipboard)
        {
            if (NativeMethods.ShiftPressed())
            {
                _clearClipboardPending = true;
                _viewState.RequestConfirmation();
                ShowStatus("クリップボード履歴をすべて削除しますか？", "復元できません。Enterで削除 / Escで戻る");
            }
            else if (ResultsList.SelectedItem is ResultRow clipboardRow && clipboardRow.Result.Id.StartsWith("clipboard:", StringComparison.Ordinal))
            {
                _clipboardHistory.Delete(clipboardRow.Result.Id[10..]);
                ApplyClipboardResults(QueryBox.Text);
            }
            return true;
        }
        return false;
    }

    private void HandleEnter(ResultRow row)
    {
        if (_viewState.State == LauncherViewState.Running) return;
        if (_viewState.State == LauncherViewState.ActionInput)
        {
            if (_actionInputFlow is null || !_actionInputFlow.Submit(QueryBox.Text)) return;
            if (!_actionInputFlow.Complete)
            {
                QueryBox.Text = string.Empty;
                QueryPlaceholder.Text = _actionInputFlow.Current!.Title;
                ShowStatus(_selectedAction!.Title, $"{_actionInputFlow.ParameterIndex + 1}/{_selectedAction.Parameters.Length} を入力");
                return;
            }
            _actionArgument = _selectedAction!.Parameters.Length == 0 ? null : _actionInputFlow.Values[_selectedAction.Parameters[0].Id];
            if (_selectedAction?.RequiresConfirmation == true) ShowActionConfirmation(); else RunSelectedAction();
            return;
        }
        if (_viewState.State == LauncherViewState.Confirmation)
        {
            if (_clearClipboardPending)
            {
                _clearClipboardPending = false;
                _clipboardHistory.Clear();
                _viewState.Reset();
                _viewState.EnterBrowse(BrowseCategory.Clipboard);
                ApplyClipboardResults();
            }
            else if (_pendingSystemResult is not null)
            {
                _systemActionConfirmed = true;
                var pending = _pendingSystemResult;
                _pendingSystemResult = null;
                if (_viewState.TryBeginRunning()) Execute(pending);
            }
            else RunSelectedAction();
            return;
        }
        if (row.Result.ProviderId == "windows.clipboard")
        {
            ClipboardTextService.Set(row.Result.CopyText ?? row.Result.ExecutionToken ?? string.Empty);
            _appWindow.Hide();
            return;
        }
        if (row.Result.ProviderId == ActionProviderId)
        {
            SelectAction(row.Result.ExecutionToken!);
            return;
        }
        if (row.Result.ProviderId == SystemActionSearchProvider.Id && SystemActionService.RequiresConfirmation(row.Result.ExecutionToken!))
        {
            _pendingSystemResult = row.Result;
            _viewState.RequestConfirmation();
            ShowStatus($"{row.Result.Title}を実行しますか？", "Enterで実行 / Escで戻る");
            return;
        }
        if (TryQuickKey(QueryBox.Text, out _, out var quickAction) && !string.IsNullOrWhiteSpace(row.Result.FilePath))
        {
            _actionTarget = row.Result;
            _selectedAction = quickAction;
            SelectAction(quickAction.Id);
            return;
        }
        Execute(row.Result);
    }

    private void OpenActions(SearchResultDto target)
    {
        var targetKind = ActionTargetClassifier.From(target);
        if (targetKind == ActionTargetKind.None || string.IsNullOrWhiteSpace(ActionSource(target))) return;
        var actions = BuiltInActions.For(targetKind);
        _actionTarget = target;
        _viewState.OpenContextActions();
        var hintCount = R1Storage.Get("ContextActionHintCount", 0);
        ScopeChipText.Text = $"{target.Title} › アクション" + (hintCount < 3 ? " · Escで戻る" : string.Empty);
        ScopeChipClose.Visibility = Visibility.Collapsed;
        ScopeChip.Visibility = Visibility.Visible;
        if (hintCount < 3) R1Storage.Set("ContextActionHintCount", hintCount + 1);
        ApplyResults(actions.Select(action => ActionResult(action, targetKind)).ToArray());
        ResizeForResults(actions.Count);
        QueryPlaceholder.Text = "アクションを選択";
    }

    private async void SelectAction(string actionId)
    {
        _selectedAction = BuiltInActions.All.FirstOrDefault(x => x.Id == actionId);
        if (_selectedAction is null || _actionTarget is null) return;
        if (_selectedAction.Parameters.Length > 0)
        {
            var selectedAction = _selectedAction;
            _actionInputFlow = new ActionInputFlow(selectedAction);
            _viewState.BeginActionInput();
            QueryBox.Text = string.Empty;
            QueryPlaceholder.Text = _actionInputFlow.Current!.Title;
            ShowStatus(selectedAction.Title, "入力後にEnter");
            if (_actionInputFlow.Current?.Kind == ActionParameterKind.FolderPath)
            {
                var pickedFolder = await PickFolderAsync();
                if (!string.IsNullOrWhiteSpace(pickedFolder) &&
                    _viewState.State == LauncherViewState.ActionInput &&
                    ReferenceEquals(_selectedAction, selectedAction))
                    QueryBox.Text = pickedFolder;
            }
        }
        else if (_selectedAction.RequiresConfirmation) ShowActionConfirmation();
        else RunSelectedAction();
    }

    private void ShowActionConfirmation()
    {
        _viewState.RequestConfirmation();
        ShowStatus($"{_selectedAction!.Title}を実行しますか？", "Enterで実行 / Escで戻る");
    }

    private async void RunSelectedAction()
    {
        var source = _actionTarget is null ? null : ActionSource(_actionTarget);
        if (_selectedAction is null || string.IsNullOrWhiteSpace(source) || !_viewState.TryBeginRunning()) return;
        _runningCancellation?.Dispose();
        _runningCancellation = new CancellationTokenSource();
        var cancellationToken = _runningCancellation.Token;
        ShowStatus(_selectedAction.Title, "実行中…");
        ActionExecutionResult result;
        try
        {
            result = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return BuiltInActionService.Execute(_selectedAction.Id, source, _actionArgument, _windowHandle, cancellationToken);
            }, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            ApplyResults([]);
            SetLauncherStatus("操作をキャンセルしました。");
            return;
        }
        if (cancellationToken.IsCancellationRequested) return;
        if (result.Success) _usageHistory.Record($"action:{_selectedAction.Id}", _activeProcessName, "Action");
        if (result.Success) ShowStatus(_selectedAction.Title, "完了");
        else
        {
            R1Storage.WriteLog($"ERROR Action {_selectedAction.Id} failed: {result.FailureReason ?? "原因不明"}");
            ApplyResults([]);
            SetLauncherStatus(
                $"実行できませんでした: {result.FailureReason ?? "原因不明"}",
                retryAction: () => { _viewState.Back(false); RunSelectedAction(); });
        }
    }

    private void EnterBrowse(BrowseCategory category)
    {
        _viewState.Reset();
        _viewState.EnterBrowse(category);
        DisplayBrowse(category);
    }

    private void DisplayBrowse(BrowseCategory category)
    {
        QueryBox.Text = string.Empty;
        QueryPlaceholder.Text = category switch
        {
            BrowseCategory.Applications => "アプリ",
            BrowseCategory.Files => "最近のファイル",
            BrowseCategory.Actions => "アクション",
            _ => "クリップボード",
        };
        if (category == BrowseCategory.Actions)
            ApplyActionResults();
        else if (category == BrowseCategory.Clipboard)
            ApplyClipboardResults();
        else if (category == BrowseCategory.Files)
            ApplyResults(WindowsRecentFiles.Get().ToArray());
        else
            _ = ApplyApplicationBrowseAsync();
        ResizeForResults(_results.Count);
    }

    private async Task ApplyApplicationBrowseAsync()
    {
        var applications = await _appSearchProvider.BrowseAsync();
        if (_viewState.BrowseCategory != BrowseCategory.Applications || QueryBox.Text.Length != 0) return;
        var rows = applications
            .OrderByDescending(x => _usageHistory.Get(x.Id)?.SelectionCount ?? 0)
            .ThenBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase)
            .Take(8).ToArray();
        ApplyResults(rows);
        ResizeForResults(rows.Length);
    }

    private void ApplyActionResults(string query = "")
    {
        var rows = SystemActionSearchProvider.Browse(query)
            .OrderByDescending(x => _usageHistory.Get(x.Id)?.SelectionCount ?? 0)
            .ToArray();
        ApplyResults(rows);
        ResizeForResults(rows.Length);
    }

    private void ApplyClipboardResults(string query = "")
    {
        var rows = _clipboardHistory.Enabled
            ? _clipboardHistory.Items.Where(x => x.Content.Contains(query, StringComparison.CurrentCultureIgnoreCase)).Take(8).Select(x => new SearchResultDto
            {
                Id = $"clipboard:{x.Id}", ProviderId = "windows.clipboard", Title = x.Content.Split('\n')[0],
                Subtitle = x.CreatedAt.LocalDateTime.ToString("g", System.Globalization.CultureInfo.CurrentCulture), Kind = ResultKind.Action,
                Icon = new(IconSource.FluentGlyph, "\uE8C8"), ExecutionToken = x.Content, CopyText = x.Content,
            }).ToArray()
            : [StatusResult("クリップボード履歴は無効です", "トレイメニューから有効化できます")];
        ApplyResults(rows);
    }

    private void SetScope(QueryScopeSelection scope)
    {
        _queryScope = scope;
        ScopeChipText.Text = scope.Token;
        ScopeChipClose.Visibility = Visibility.Visible;
        ScopeChip.Visibility = Visibility.Visible;
        QueryBox.Text = string.Empty;
        ResizeForResults(_results.Count);
    }

    private void OnScopeChipClose(object sender, RoutedEventArgs args) => ClearScope();
    private void ClearScope()
    {
        _queryScope = null;
        ScopeChip.Visibility = Visibility.Collapsed;
        StartSearch(QueryBox.Text);
    }

    private void RestoreScopeChip()
    {
        if (_queryScope is null)
        {
            ScopeChip.Visibility = Visibility.Collapsed;
            return;
        }
        ScopeChipText.Text = _queryScope.Value.Token;
        ScopeChipClose.Visibility = Visibility.Visible;
        ScopeChip.Visibility = Visibility.Visible;
    }
    private void RestoreState()
    {
        if (_viewState.State == LauncherViewState.Search)
        {
            QueryPlaceholder.Text = "Anything...";
            RestoreScopeChip();
            StartSearch(QueryBox.Text);
        }
        else if (_viewState.State == LauncherViewState.Browse && _viewState.BrowseCategory is { } category) DisplayBrowse(category);
        else if (_viewState.State == LauncherViewState.ContextActions && _actionTarget is not null) OpenActions(_actionTarget);
        else if (_viewState.State == LauncherViewState.ActionInput && _selectedAction is not null && _actionInputFlow is not null)
        {
            QueryBox.Text = _actionInputFlow.Rewind();
            QueryPlaceholder.Text = _actionInputFlow.Current?.Title ?? "引数";
            ShowStatus(_selectedAction.Title, "入力後にEnter");
        }
        else if (_viewState.State == LauncherViewState.Confirmation && _selectedAction is not null)
            ShowStatus($"{_selectedAction.Title}を実行しますか？", "Enterで実行 / Escで戻る");
    }

    private void ShowStatus(string title, string subtitle)
    {
        ApplyResults([StatusResult(title, subtitle)]);
        ResizeForResults(1);
    }

    private static SearchResultDto StatusResult(string title, string subtitle) => new()
    {
        Id = $"status:{title}", ProviderId = StatusProviderId, Title = title, Subtitle = subtitle,
        Kind = ResultKind.Action, Icon = new(IconSource.FluentGlyph, "\uE946"),
    };

    private static SearchResultDto ActionResult(ActionDescriptor action, ActionTargetKind targetKind = ActionTargetKind.None) => new()
    {
        Id = $"action:{action.Id}", ProviderId = ActionProviderId,
        Title = action.Id == "copy-path" && targetKind == ActionTargetKind.Url ? "URLをコピー" : action.Title,
        Subtitle = action.RequiresConfirmation ? "確認あり" : "内蔵アクション", Kind = ResultKind.Action,
        Icon = new(IconSource.FluentGlyph, action.Glyph), ExecutionToken = action.Id,
    };

    private bool TryQuickKey(string query, out string searchText, out ActionDescriptor action)
    {
        var parts = query.TrimEnd().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        _quickKeys.Load();
        action = parts.Length > 1 ? _quickKeys.FindAction(parts[^1])! : null!;
        searchText = action is null ? query : string.Join(' ', parts[..^1]);
        return action is not null;
    }

    private void UpdateGhostCompletion(string query)
    {
        var last = query.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
        var mappings = _quickKeys.Load();
        GhostCompletion.Text = last.Length == 0
            ? string.Empty
            : mappings.Keys.FirstOrDefault(key =>
                key.StartsWith(last, StringComparison.OrdinalIgnoreCase) && key.Length > last.Length) ?? string.Empty;
    }

    private void OnResultSelectionChanged(object sender, SelectionChangedEventArgs args) => UpdateQuickKeyBadges();

    private void UpdateQuickKeyBadges()
    {
        _quickKeys.Load();
        foreach (var row in _results)
        {
            var actionId = row.Result.Kind switch
            {
                ResultKind.Folder => "terminal",
                ResultKind.Application or ResultKind.File => "reveal",
                ResultKind.Url or ResultKind.WebSearch => "copy-path",
                _ => null,
            };
            row.SetQuickKey(ReferenceEquals(row, ResultsList.SelectedItem) && actionId is not null
                ? _quickKeys.FindKey(actionId)
                : null);
        }
    }

    private void SetLauncherStatus(string message, bool retry = false, bool resize = true, Action? retryAction = null)
    {
        _statusAutoHideTimer?.Stop();
        _statusRetry = retryAction ?? (retry ? () => StartSearch(QueryBox.Text) : null);
        StatusText.Text = message;
        StatusRetryButton.Visibility = _statusRetry is null ? Visibility.Collapsed : Visibility.Visible;
        StatusRow.Visibility = Visibility.Visible;
        if (resize) ResizeForResults(_results.Count);
    }

    private void ClearLauncherStatus(bool resize = true)
    {
        _statusAutoHideTimer?.Stop();
        if (StatusRow.Visibility == Visibility.Collapsed) return;
        _statusRetry = null;
        StatusRetryButton.Visibility = Visibility.Collapsed;
        StatusRow.Visibility = Visibility.Collapsed;
        if (resize) ResizeForResults(_results.Count);
    }

    private void ShowEverythingUnavailableStatus(bool force = false)
    {
        if (_everythingStatusShown && !force)
        {
            ClearLauncherStatus();
            return;
        }
        _everythingStatusShown = true;
        ShowTransientLauncherStatus(
            "Everythingは未接続です。Windows Indexで検索しています。",
            () => _ = RefreshEverythingAvailabilityAsync(true));
    }

    private void ShowTransientLauncherStatus(string message, Action? retryAction = null)
    {
        SetLauncherStatus(message, retryAction: retryAction);
        _statusAutoHideTimer ??= DispatcherQueue.CreateTimer();
        _statusAutoHideTimer.IsRepeating = false;
        _statusAutoHideTimer.Interval = TimeSpan.FromMilliseconds(Token("TransientStatusDurationMilliseconds"));
        _statusAutoHideTimer.Tick -= OnStatusAutoHideTimerTick;
        _statusAutoHideTimer.Tick += OnStatusAutoHideTimerTick;
        _statusAutoHideTimer.Start();
    }
    private void OnStatusAutoHideTimerTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args) =>
        ClearLauncherStatus();
    private void OnStatusRetry(object sender, RoutedEventArgs args) => _statusRetry?.Invoke();
    private async Task<string?> PickFolderAsync()
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, _windowHandle);
            return (await picker.PickSingleFolderAsync())?.Path;
        }
        catch (Exception exception)
        {
            R1Storage.WriteLog($"WARN Folder picker failed; using text fallback: {exception.Message}");
            return null;
        }
    }

    private static string? ActionSource(SearchResultDto target) =>
        target.FilePath ?? target.CopyText ?? target.ExecutionToken;

    private static QuickKeyRegistry CreateQuickKeyRegistry() => new(
        () => R1Storage.Get<Dictionary<string, string>?>("QuickKeys", null),
        mappings => R1Storage.Set("QuickKeys", new Dictionary<string, string>(mappings, StringComparer.OrdinalIgnoreCase)));
}
