using System.Security.Cryptography;
using System.Text;

namespace Beacon.Core;

public enum ClipboardContentKind { Text, Url, Files, Html }
public sealed record ClipboardHistoryItem(string Id, ClipboardContentKind Kind, string Content, DateTimeOffset CreatedAt, string Hash);

public sealed class ClipboardHistory
{
    public const int MaximumItems = 500;
    public static readonly TimeSpan Retention = TimeSpan.FromDays(7);
    private readonly List<ClipboardHistoryItem> _items;

    public ClipboardHistory(IEnumerable<ClipboardHistoryItem>? items = null) => _items = items?.ToList() ?? [];
    public IReadOnlyList<ClipboardHistoryItem> Items => _items;

    public bool Add(ClipboardContentKind kind, string content, DateTimeOffset? now = null)
    {
        if (string.IsNullOrEmpty(content)) return false;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
        if (_items.Any(x => x.Hash == hash)) return false;
        var created = now ?? DateTimeOffset.Now;
        _items.Insert(0, new(Guid.NewGuid().ToString("N"), kind, content, created, hash));
        _items.RemoveAll(x => created - x.CreatedAt > Retention);
        if (_items.Count > MaximumItems) _items.RemoveRange(MaximumItems, _items.Count - MaximumItems);
        return true;
    }

    public bool Delete(string id) => _items.RemoveAll(x => x.Id == id) > 0;
    public void Clear() => _items.Clear();
}
