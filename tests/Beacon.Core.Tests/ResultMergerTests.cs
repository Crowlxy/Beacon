using Beacon.Contracts;
using Beacon.Core;
using NUnit.Framework;

namespace Beacon.Core.Tests;

public sealed class ResultMergerTests
{
    private static SearchResultDto Result(string id, double score) => new()
    {
        Id = id,
        ProviderId = "test",
        Title = id,
        Score = score,
    };

    [Test]
    public void FirstMergeWithEmptyCommittedAdoptsAllCandidatesByDescendingScore()
    {
        var candidates = new Dictionary<string, SearchResultDto>
        {
            ["a"] = Result("a", 10),
            ["b"] = Result("b", 30),
            ["c"] = Result("c", 20),
        };

        var display = ResultMerger.Merge([], candidates, max: 10);

        string[] expected = ["b", "c", "a"];
        Assert.That(display.Select(r => r.Id), Is.EqualTo(expected));
    }

    [Test]
    public void LateArrivalsAreAppendedAfterExistingRowsWithoutReorderingThem()
    {
        var committed = new List<string> { "b", "c", "a" };
        var candidates = new Dictionary<string, SearchResultDto>
        {
            ["a"] = Result("a", 10),
            ["b"] = Result("b", 30),
            ["c"] = Result("c", 20),
            ["d"] = Result("d", 999), // arrives late with the highest score
        };

        var display = ResultMerger.Merge(committed, candidates, max: 10);

        string[] expected = ["b", "c", "a", "d"];
        Assert.That(display.Select(r => r.Id), Is.EqualTo(expected));
    }

    [Test]
    public void RelativeOrderOfAlreadyShownRowsIsStableAcrossSubsequentMerges()
    {
        var committed = new List<string> { "b", "c", "a" };
        var candidates = new Dictionary<string, SearchResultDto>
        {
            // Scores now favor "a" over "b"/"c", but committed rows must not reshuffle.
            ["a"] = Result("a", 500),
            ["b"] = Result("b", 30),
            ["c"] = Result("c", 20),
            ["d"] = Result("d", 999),
            ["e"] = Result("e", 1),
        };

        var display = ResultMerger.Merge(committed, candidates, max: 10);

        string[] expected = ["b", "c", "a", "d", "e"];
        Assert.That(display.Select(r => r.Id), Is.EqualTo(expected));
    }

    [Test]
    public void HighScoringLateArrivalsDoNotDisplaceAlreadyVisibleRowsOnceMaxIsReached()
    {
        var committed = new List<string> { "a", "b" };
        var candidates = new Dictionary<string, SearchResultDto>
        {
            ["a"] = Result("a", 1),
            ["b"] = Result("b", 1),
            ["c"] = Result("c", 9999), // arrives late with a very high score
        };

        var display = ResultMerger.Merge(committed, candidates, max: 2);

        string[] expected = ["a", "b"];
        Assert.That(display.Select(r => r.Id), Is.EqualTo(expected));
    }

    [Test]
    public void CandidateRetentionLimitIsIndependentFromVisibleRowLimit()
    {
        const int maximumVisibleCount = 7;
        const int maximumCandidateCount = 30;
        var candidates = Enumerable.Range(1, maximumCandidateCount)
            .ToDictionary(
                index => $"result-{index:D2}",
                index => Result($"result-{index:D2}", maximumCandidateCount - index));

        var retained = ResultMerger.Merge([], candidates, maximumCandidateCount);
        var visible = retained.Take(maximumVisibleCount).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(retained, Has.Count.EqualTo(maximumCandidateCount));
            Assert.That(visible, Has.Length.EqualTo(maximumVisibleCount));
            Assert.That(retained.Skip(maximumVisibleCount), Is.Not.Empty);
        });
    }

    [Test]
    public void CommittedIdsMissingFromCandidatesAreDroppedInsteadOfThrowing()
    {
        var committed = new List<string> { "a", "ghost", "b" };
        var candidates = new Dictionary<string, SearchResultDto>
        {
            ["a"] = Result("a", 10),
            ["b"] = Result("b", 5),
        };

        IReadOnlyList<SearchResultDto>? display = null;
        Assert.DoesNotThrow(() => display = ResultMerger.Merge(committed, candidates, max: 10));
        string[] expected = ["a", "b"];
        Assert.That(display!.Select(r => r.Id), Is.EqualTo(expected));
    }
}
