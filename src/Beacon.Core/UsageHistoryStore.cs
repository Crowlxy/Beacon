using System.Text;
using System.Text.Json;

namespace Beacon.Core;

public sealed record UsageHistoryEntry(
    string ResultId,
    int SelectionCount,
    DateTimeOffset LastSelectedAt,
    string? ActiveProcessName,
    string Mode);

public sealed class UsageHistoryStore
{
    private readonly string _path;
    private readonly Action<string>? _log;
    private readonly object _gate = new();
    private Dictionary<string, UsageHistoryEntry> _entries;

    public UsageHistoryStore(string dataRoot, Action<string>? log = null)
    {
        _path = Path.Combine(dataRoot, "History", "usage.json");
        _log = log;
        _entries = Load(_path, log);
    }

    public bool Enabled { get; set; } = true;
    public int MaximumSelectionCount { get { lock (_gate) return _entries.Count == 0 ? 0 : _entries.Values.Max(x => x.SelectionCount); } }
    public UsageHistoryEntry? Get(string resultId) { lock (_gate) return _entries.GetValueOrDefault(resultId); }

    public void Record(string resultId, string? activeProcessName, string mode, DateTimeOffset? now = null)
    {
        if (!Enabled) return;
        lock (_gate)
        {
            var previous = _entries.GetValueOrDefault(resultId);
            _entries[resultId] = new(resultId, (previous?.SelectionCount ?? 0) + 1, now ?? DateTimeOffset.Now, activeProcessName, mode);
            Save();
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _entries.Clear();
            Save();
        }
    }

    private void Save()
    {
        try
        {
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(_entries.Values), new UTF8Encoding(false));
            File.Move(temporary, _path, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _log?.Invoke($"WARN Usage history write failed: {exception.Message}");
        }
    }

    private static Dictionary<string, UsageHistoryEntry> Load(string path, Action<string>? log)
    {
        try
        {
            return File.Exists(path)
                ? (JsonSerializer.Deserialize<UsageHistoryEntry[]>(File.ReadAllText(path)) ?? []).ToDictionary(x => x.ResultId)
                : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            log?.Invoke($"WARN Usage history read failed: {exception.Message}");
            return [];
        }
    }
}
