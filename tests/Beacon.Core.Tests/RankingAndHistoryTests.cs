using Beacon.Contracts;
using Beacon.Core;
using NUnit.Framework;

namespace Beacon.Core.Tests;

public sealed class RankingAndHistoryTests
{
    [Test]
    public void RankingAppliesEntireScoreTable()
    {
        var now = new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);
        var result = Result("Beacon", ResultKind.Application, @"C:\Work\Beacon.exe");
        var usage = new UsageHistoryEntry(result.Id, 10, now.AddHours(-1), "explorer", "Search");

        var score = RankingEngine.Score(result, "Beacon", usage, 10, new(now, "explorer", @"C:\Work"));

        Assert.That(score, Is.EqualTo(600 + 120 + 180 + 80 + 80));
    }

    [Test]
    public void RankingUsesPrefixTitleAndSevenDayBands()
    {
        var now = DateTimeOffset.UtcNow;
        var usage = new UsageHistoryEntry("id", 1, now.AddDays(-2), null, "Search");
        Assert.That(RankingEngine.Score(Result("Beacon Launcher"), "Beacon", usage, 2, new(now)), Is.EqualTo(300 + 60 + 90));
        Assert.That(RankingEngine.Score(Result("Open Beacon Launcher"), "Beacon", null, 0, new(now)), Is.EqualTo(150));
    }

    [Test]
    public void WebPenaltyIsExactlyMinus250AndBelowLocalResult()
    {
        var context = new RankingContext(DateTimeOffset.UtcNow);
        var local = RankingEngine.Score(Result("local"), "query", null, 0, context);
        var web = RankingEngine.Score(Result("web", ResultKind.WebSearch, score: -250), "query", null, 0, context);
        Assert.Multiple(() =>
        {
            Assert.That(web, Is.EqualTo(-250));
            Assert.That(web, Is.LessThan(local));
        });
    }

    [Test]
    public void HistoryPersistsOnlyAllowedFieldsAndCanReset()
    {
        var root = Path.Combine(Path.GetTempPath(), $"Beacon-history-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "History"));
        try
        {
            var store = new UsageHistoryStore(root);
            store.Record("result", "explorer", "Files", DateTimeOffset.UnixEpoch);
            var loaded = new UsageHistoryStore(root);
            Assert.That(loaded.Get("result"), Is.EqualTo(new UsageHistoryEntry("result", 1, DateTimeOffset.UnixEpoch, "explorer", "Files")));
            loaded.Reset();
            Assert.That(loaded.Get("result"), Is.Null);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static SearchResultDto Result(string title, ResultKind kind = ResultKind.Application, string? path = null, double score = 0) => new()
    {
        Id = "id", ProviderId = "provider", Title = title, Kind = kind, FilePath = path, Score = score,
    };
}
