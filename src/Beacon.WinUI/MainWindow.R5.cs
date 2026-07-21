using Beacon.Contracts;
using Beacon.Core;
using Beacon.Platform.Windows;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Beacon.WinUI;

public sealed partial class MainWindow
{
    private const string ActionProviderId = "beacon.actions";
    private const string StatusProviderId = "beacon.status";
    private readonly LauncherStateMachine _viewState = new();
    private QueryScopeSelection? _queryScope;
    private SearchResultDto? _actionTarget;
    private ActionDescriptor? _selectedAction;
    private string? _actionArgument;
    private SearchResultDto? _pendingSystemResult;
    private bool _systemActionConfirmed;

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
            switch (_viewState.Back(QueryBox.Text.Length > 0))
            {
                case BackOutcome.Close: _appWindow.Hide(); break;
                case BackOutcome.ClearQuery: QueryBox.Text = string.Empty; break;
                case BackOutcome.StateChanged: RestoreState(); break;
            }
            return true;
        }
        if (args.Key == VirtualKey.Right && ResultsList.SelectedItem is ResultRow contextRow)
        {
            OpenActions(contextRow.Result);
            return true;
        }
        return false;
    }

    private void HandleEnter(ResultRow row)
    {
        if (_viewState.State == LauncherViewState.Running) return;
        if (_viewState.State == LauncherViewState.ActionInput)
        {
            if (_selectedAction?.Parameters.Any(x => x.Required) == true && string.IsNullOrWhiteSpace(QueryBox.Text)) return;
            _actionArgument = QueryBox.Text;
            if (_selectedAction?.RequiresConfirmation == true) ShowActionConfirmation(); else RunSelectedAction();
            return;
        }
        if (_viewState.State == LauncherViewState.Confirmation)
        {
            if (_pendingSystemResult is not null)
            {
                _systemActionConfirmed = true;
                var pending = _pendingSystemResult;
                _pendingSystemResult = null;
                if (_viewState.TryBeginRunning()) Execute(pending);
            }
            else RunSelectedAction();
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
        if (string.IsNullOrWhiteSpace(target.FilePath)) return;
        _actionTarget = target;
        _viewState.OpenContextActions();
        ApplyResults(BuiltInActions.All.Select(ActionResult).ToArray());
        ResizeForResults(BuiltInActions.All.Count);
        QueryPlaceholder.Text = "アクションを選択";
    }

    private void SelectAction(string actionId)
    {
        _selectedAction = BuiltInActions.All.FirstOrDefault(x => x.Id == actionId);
        if (_selectedAction is null || _actionTarget is null) return;
        if (_selectedAction.Parameters.Length > 0)
        {
            _viewState.BeginActionInput();
            QueryBox.Text = string.Empty;
            QueryPlaceholder.Text = _selectedAction.Parameters[0].Title;
            ShowStatus(_selectedAction.Title, "入力後にEnter");
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
        if (_selectedAction is null || _actionTarget?.FilePath is null || !_viewState.TryBeginRunning()) return;
        ShowStatus(_selectedAction.Title, "実行中…");
        var result = await Task.Run(() => BuiltInActionService.Execute(_selectedAction.Id, _actionTarget.FilePath, _actionArgument));
        ShowStatus(_selectedAction.Title, result.Success ? "完了" : result.FailureReason ?? "失敗");
    }

    private void EnterBrowse(BrowseCategory category)
    {
        _viewState.Reset();
        _viewState.EnterBrowse(category);
        QueryBox.Text = string.Empty;
        QueryPlaceholder.Text = category switch
        {
            BrowseCategory.Applications => "アプリ",
            BrowseCategory.Files => "最近のファイル",
            BrowseCategory.Actions => "アクション",
            _ => "クリップボード",
        };
        if (category == BrowseCategory.Actions)
            ApplyResults(BuiltInActions.All.Select(ActionResult).ToArray());
        else if (category == BrowseCategory.Clipboard)
            ApplyClipboardResults();
        else
            ShowStatus(QueryPlaceholder.Text, "入力して絞り込み");
        ResizeForResults(_results.Count);
    }

    private void ApplyClipboardResults()
    {
        var rows = _clipboardHistory.Enabled
            ? _clipboardHistory.Items.Take(8).Select(x => new SearchResultDto
            {
                Id = $"clipboard:{x.Id}", ProviderId = "windows.clipboard", Title = x.Content.Split('\n')[0],
                Subtitle = x.CreatedAt.LocalDateTime.ToString("g"), Kind = ResultKind.Action,
                Icon = new(IconSource.FluentGlyph, "\uE8C8"), ExecutionToken = x.Content, CopyText = x.Content,
            }).ToArray()
            : [StatusResult("クリップボード履歴は無効です", "トレイメニューから有効化できます")];
        ApplyResults(rows);
    }

    private void SetScope(QueryScopeSelection scope)
    {
        _queryScope = scope;
        ScopeChipText.Text = scope.Token;
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

    private void RestoreState()
    {
        if (_viewState.State == LauncherViewState.Search)
        {
            QueryPlaceholder.Text = "Anything...";
            StartSearch(QueryBox.Text);
        }
        else if (_viewState.State == LauncherViewState.Browse && _viewState.BrowseCategory is { } category) EnterBrowse(category);
        else if (_viewState.State == LauncherViewState.ContextActions && _actionTarget is not null) OpenActions(_actionTarget);
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

    private static SearchResultDto ActionResult(ActionDescriptor action) => new()
    {
        Id = $"action:{action.Id}", ProviderId = ActionProviderId, Title = action.Title,
        Subtitle = action.RequiresConfirmation ? "確認あり" : "内蔵アクション", Kind = ResultKind.Action,
        Icon = new(IconSource.FluentGlyph, action.Glyph), ExecutionToken = action.Id,
    };

    private static bool TryQuickKey(string query, out string searchText, out ActionDescriptor action)
    {
        var parts = query.TrimEnd().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        action = parts.Length > 1 ? BuiltInActions.FindQuickKey(parts[^1])! : null!;
        searchText = action is null ? query : string.Join(' ', parts[..^1]);
        return action is not null;
    }

    private void UpdateGhostCompletion(string query)
    {
        var last = query.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
        GhostCompletion.Text = BuiltInActions.All.Select(x => x.QuickKey).FirstOrDefault(x => x is not null && x.StartsWith(last, StringComparison.OrdinalIgnoreCase) && x.Length > last.Length) ?? string.Empty;
    }
}
