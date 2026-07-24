using System.Text;
using System.Text.Json;
using Beacon.Core;
using NUnit.Framework;

namespace Beacon.Core.Tests;

public sealed class UsageHistoryStoreTests
{
    [Test]
    public void LoadIgnoresDuplicateResultIdsAndKeepsTheLastEntry()
    {
        var root = CreateTempDataRoot();
        try
        {
            var historyPath = HistoryFilePath(root);
            var first = new UsageHistoryEntry("result", 1, DateTimeOffset.UnixEpoch, null, "Search");
            var last = new UsageHistoryEntry("result", 9, DateTimeOffset.UnixEpoch.AddDays(1), "explorer", "Files");
            WriteRawHistory(historyPath, [first, last]);

            var store = new UsageHistoryStore(root);

            Assert.That(store.Get("result"), Is.EqualTo(last));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public void LoadIgnoresEntriesWithNullResultId()
    {
        var root = CreateTempDataRoot();
        try
        {
            var historyPath = HistoryFilePath(root);
            var valid = new UsageHistoryEntry("result", 1, DateTimeOffset.UnixEpoch, null, "Search");
            var json = $$"""
                [
                    {"ResultId": null, "SelectionCount": 5, "LastSelectedAt": "2026-07-21T00:00:00+00:00", "ActiveProcessName": null, "Mode": "Search"},
                    {"ResultId": "result", "SelectionCount": 1, "LastSelectedAt": "1970-01-01T00:00:00+00:00", "ActiveProcessName": null, "Mode": "Search"}
                ]
                """;
            File.WriteAllText(historyPath, json, new UTF8Encoding(false));

            UsageHistoryStore store = null!;
            Assert.DoesNotThrow(() => store = new UsageHistoryStore(root));
            Assert.Multiple(() =>
            {
                Assert.That(store.Get("result"), Is.EqualTo(valid));
                Assert.That(store.Snapshot(), Has.Count.EqualTo(1));
            });
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public void CalculatorNegativeAndExponentEntriesRoundTrip()
    {
        var root = CreateTempDataRoot();
        try
        {
            var store = new UsageHistoryStore(root);
            store.Record("calculation:1+2+3", null, "Search", DateTimeOffset.UnixEpoch);
            store.Record("calculation:-5*2", null, "Search", DateTimeOffset.UnixEpoch);
            store.Record($"calculation:{1E+15}", null, "Search", DateTimeOffset.UnixEpoch);

            var reloaded = new UsageHistoryStore(root);

            Assert.Multiple(() =>
            {
                Assert.That(reloaded.Get("calculation:1+2+3"), Is.Not.Null);
                Assert.That(reloaded.Get("calculation:-5*2"), Is.Not.Null);
                Assert.That(reloaded.Get($"calculation:{1E+15}"), Is.Not.Null);
                Assert.That(reloaded.Snapshot(), Has.Count.EqualTo(3));
            });
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateTempDataRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"Beacon-usage-history-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "History"));
        return root;
    }

    private static string HistoryFilePath(string root) => Path.Combine(root, "History", "usage.json");

    private static void WriteRawHistory(string path, UsageHistoryEntry[] entries) =>
        File.WriteAllText(path, JsonSerializer.Serialize(entries), new UTF8Encoding(false));
}
