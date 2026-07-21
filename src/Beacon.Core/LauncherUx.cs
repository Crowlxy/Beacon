using Beacon.Contracts;

namespace Beacon.Core;

public enum LauncherViewState { Search, Browse, ContextActions, ActionInput, Confirmation, Running }
public enum BrowseCategory { Applications, Files, Actions, Clipboard }
public enum BackOutcome { StateChanged, ClearQuery, Close }
public enum ActionParameterKind { Text, FilePath, FolderPath, Choice }

public sealed class LauncherStateMachine
{
    private readonly Stack<LauncherViewState> _history = new();
    public LauncherViewState State { get; private set; } = LauncherViewState.Search;
    public BrowseCategory? BrowseCategory { get; private set; }

    public void EnterBrowse(BrowseCategory category)
    {
        BrowseCategory = category;
        Transition(LauncherViewState.Browse);
    }

    public void OpenContextActions() => Transition(LauncherViewState.ContextActions);
    public void BeginActionInput() => Transition(LauncherViewState.ActionInput);
    public void RequestConfirmation() => Transition(LauncherViewState.Confirmation);

    public bool TryBeginRunning()
    {
        if (State == LauncherViewState.Running) return false;
        Transition(LauncherViewState.Running);
        return true;
    }

    public BackOutcome Back(bool hasSearchInput)
    {
        if (_history.TryPop(out var previous))
        {
            State = previous;
            if (State == LauncherViewState.Search) BrowseCategory = null;
            return BackOutcome.StateChanged;
        }
        return hasSearchInput ? BackOutcome.ClearQuery : BackOutcome.Close;
    }

    public void Reset()
    {
        _history.Clear();
        State = LauncherViewState.Search;
        BrowseCategory = null;
    }

    private void Transition(LauncherViewState next)
    {
        if (State == next) return;
        _history.Push(State);
        State = next;
    }
}

public readonly record struct QueryScopeSelection(string Token, QueryScope? ProviderScope, bool IsClipboard)
{
    private static readonly Dictionary<string, QueryScopeSelection> Values = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/app"] = new("/app", QueryScope.Applications, false),
        ["/アプリ"] = new("/app", QueryScope.Applications, false),
        ["/file"] = new("/file", QueryScope.Files, false),
        ["/ファイル"] = new("/file", QueryScope.Files, false),
        ["/folder"] = new("/folder", QueryScope.Folders, false),
        ["/フォルダ"] = new("/folder", QueryScope.Folders, false),
        ["/action"] = new("/action", QueryScope.Actions, false),
        ["/setting"] = new("/setting", QueryScope.Settings, false),
        ["/clipboard"] = new("/clipboard", null, true),
    };

    public static bool TryParse(string text, out QueryScopeSelection selection) => Values.TryGetValue(text.Trim(), out selection);
}

public sealed record ActionParameter(string Id, string Title, ActionParameterKind Kind, bool Required, string[]? Choices = null);
public sealed record ActionDescriptor(
    string Id,
    string Title,
    string Glyph,
    ActionParameter[] Parameters,
    bool RequiresConfirmation = false,
    string? QuickKey = null);

public static class BuiltInActions
{
    public static IReadOnlyList<ActionDescriptor> All { get; } =
    [
        new("open", "開く", "\uE8E5", []),
        new("reveal", "保存場所を表示", "\uE838", [], QuickKey: "rf"),
        new("copy-path", "パスをコピー", "\uE8C8", [], QuickKey: "cp"),
        new("run-admin", "管理者として実行", "\uE7EF", []),
        new("rename", "名前変更", "\uE8AC", [new("name", "新しい名前", ActionParameterKind.Text, true)], true, "rn"),
        new("copy", "コピー", "\uE8B0", [new("destination", "コピー先", ActionParameterKind.FolderPath, true)]),
        new("move", "移動", "\uE8DE", [new("destination", "移動先", ActionParameterKind.FolderPath, true)], true),
        new("zip", "ZIP圧縮", "\uE8B7", []),
        new("terminal", "この場所でターミナル", "\uE756", [], QuickKey: "term"),
        new("open-with", "既定アプリで開く", "\uE7C4", []),
    ];

    public static ActionDescriptor? FindQuickKey(string key) => All.FirstOrDefault(x => string.Equals(x.QuickKey, key, StringComparison.OrdinalIgnoreCase));
}
