using System.Collections.ObjectModel;
using Beacon.Contracts;

namespace Beacon.Core;

public enum LauncherViewState { Search, Browse, ContextActions, ActionInput, Confirmation, Running }
public enum BrowseCategory { Applications, Files, Actions, Clipboard }
public enum BackOutcome { StateChanged, ClearQuery, Close }
public enum ActionParameterKind { Text, FilePath, FolderPath, Choice }

[Flags]
public enum ActionTargetKind
{
    None = 0,
    File = 1,
    Folder = 2,
    Application = 4,
    Url = 8,
    All = File | Folder | Application | Url,
}

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
    string? QuickKey = null,
    ActionTargetKind AppliesTo = ActionTargetKind.All);

public sealed class ActionInputFlow(ActionDescriptor descriptor)
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    public int ParameterIndex { get; private set; }
    public ActionParameter? Current => ParameterIndex < descriptor.Parameters.Length ? descriptor.Parameters[ParameterIndex] : null;
    public bool Complete => Current is null;
    public IReadOnlyDictionary<string, string> Values => _values;

    public bool Submit(string value)
    {
        var parameter = Current ?? throw new InvalidOperationException("入力は完了しています。");
        if (parameter.Required && string.IsNullOrWhiteSpace(value)) return false;
        if (parameter.Kind == ActionParameterKind.Choice && parameter.Choices is { Length: > 0 } &&
            !parameter.Choices.Contains(value, StringComparer.CurrentCultureIgnoreCase)) return false;
        _values[parameter.Id] = value;
        ParameterIndex++;
        return true;
    }

    public string Rewind()
    {
        if (ParameterIndex == 0) return string.Empty;
        ParameterIndex--;
        var parameter = descriptor.Parameters[ParameterIndex];
        var value = _values.GetValueOrDefault(parameter.Id) ?? string.Empty;
        _values.Remove(parameter.Id);
        return value;
    }
}

public static class BuiltInActions
{
    public static IReadOnlyList<ActionDescriptor> All { get; } =
    [
        new("open", "開く", "\uE8E5", [], AppliesTo: ActionTargetKind.All),
        new("reveal", "保存場所を表示", "\uE838", [], QuickKey: "rf", AppliesTo: ActionTargetKind.File | ActionTargetKind.Application),
        new("copy-path", "パスをコピー", "\uE8C8", [], QuickKey: "cp", AppliesTo: ActionTargetKind.All),
        new("run-admin", "管理者として実行", "\uE7EF", [], AppliesTo: ActionTargetKind.Application),
        new("rename", "名前変更", "\uE8AC", [new("name", "新しい名前", ActionParameterKind.Text, true)], true, "rn", ActionTargetKind.File | ActionTargetKind.Folder),
        new("copy", "コピー", "\uE8B0", [new("destination", "コピー先", ActionParameterKind.FolderPath, true)], AppliesTo: ActionTargetKind.File | ActionTargetKind.Folder),
        new("move", "移動", "\uE8DE", [new("destination", "移動先", ActionParameterKind.FolderPath, true)], true, AppliesTo: ActionTargetKind.File | ActionTargetKind.Folder),
        new("zip", "ZIP圧縮", "\uE8B7", [], AppliesTo: ActionTargetKind.File | ActionTargetKind.Folder),
        new("terminal", "この場所でターミナル", "\uE756", [], QuickKey: "term", AppliesTo: ActionTargetKind.Folder),
        new("open-with", "プログラムから開く", "\uE7C4", [], AppliesTo: ActionTargetKind.File),
    ];

    public static ActionDescriptor? FindQuickKey(string key) =>
        All.FirstOrDefault(action => string.Equals(action.QuickKey, key, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<ActionDescriptor> For(ActionTargetKind target) =>
        All.Where(action => (action.AppliesTo & target) != 0).ToArray();
}

public static class ActionTargetClassifier
{
    public static ActionTargetKind From(SearchResultDto result) => result.Kind switch
    {
        ResultKind.Application => ActionTargetKind.Application,
        ResultKind.File => ActionTargetKind.File,
        ResultKind.Folder => ActionTargetKind.Folder,
        ResultKind.Url or ResultKind.WebSearch => ActionTargetKind.Url,
        _ => ActionTargetKind.None,
    };
}

public sealed class QuickKeyRegistry(
    Func<IReadOnlyDictionary<string, string>?> load,
    Action<IReadOnlyDictionary<string, string>> save)
{
    private IReadOnlyDictionary<string, string> _mappings = DefaultMappings;

    public static IReadOnlyDictionary<string, string> DefaultMappings { get; } =
        new ReadOnlyDictionary<string, string>(
            BuiltInActions.All
                .Where(action => !string.IsNullOrWhiteSpace(action.QuickKey))
                .ToDictionary(action => action.QuickKey!, action => action.Id, StringComparer.OrdinalIgnoreCase));

    public IReadOnlyDictionary<string, string> Mappings => _mappings;

    public IReadOnlyDictionary<string, string> Load()
    {
        var stored = load();
        _mappings = stored is null ? Copy(DefaultMappings) : Normalize(stored);
        return _mappings;
    }

    public void Save(IReadOnlyDictionary<string, string> mappings)
    {
        _mappings = Normalize(mappings);
        save(_mappings);
    }

    public ActionDescriptor? FindAction(string key) =>
        _mappings.TryGetValue(key, out var actionId)
            ? BuiltInActions.All.FirstOrDefault(action => string.Equals(action.Id, actionId, StringComparison.Ordinal))
            : null;

    public string? FindKey(string actionId) =>
        _mappings.FirstOrDefault(mapping => string.Equals(mapping.Value, actionId, StringComparison.Ordinal)).Key;

    private static ReadOnlyDictionary<string, string> Normalize(IReadOnlyDictionary<string, string> mappings)
    {
        var knownActions = BuiltInActions.All.Select(action => action.Id).ToHashSet(StringComparer.Ordinal);
        var normalized = mappings
            .Where(mapping => !string.IsNullOrWhiteSpace(mapping.Key) && knownActions.Contains(mapping.Value))
            .ToDictionary(mapping => mapping.Key.Trim(), mapping => mapping.Value, StringComparer.OrdinalIgnoreCase);
        return new ReadOnlyDictionary<string, string>(normalized);
    }

    private static ReadOnlyDictionary<string, string> Copy(IReadOnlyDictionary<string, string> mappings) =>
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(mappings, StringComparer.OrdinalIgnoreCase));
}
