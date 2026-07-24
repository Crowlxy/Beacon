using Beacon.Core;
using NUnit.Framework;

namespace Beacon.Core.Tests;

public sealed class FuzzyMatcherTests
{
    [TestCase("vsc", "Visual Studio Code", new[] { 0, 7, 14 })]
    [TestCase("vs", "Visual Studio Code", new[] { 0, 7 })]
    [TestCase("VSC", "visual studio code", new[] { 0, 7, 14 })]
    [TestCase("cafe", "Café", new[] { 0, 1, 2, 3 })]
    [TestCase("設定", "設定", new[] { 0, 1 })]
    public void MatchReturnsExpectedIndices(string query, string candidate, int[] expected)
    {
        var result = FuzzyMatcher.Match(query, candidate);
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.MatchedIndices, Is.EqualTo(expected));
        });
    }

    [Test]
    public void MatchQualitySeparatesAllMatchKinds()
    {
        var exact = FuzzyMatcher.Match("beacon", "beacon").Score;
        var prefix = FuzzyMatcher.Match("beacon", "Beacon Launcher").Score;
        var wordInitial = FuzzyMatcher.Match("vs", "Visual Studio Code").Score;
        var contiguous = FuzzyMatcher.Match("studio", "Visual Studio Code").Score;
        var acronym = FuzzyMatcher.Match("vs", "VideoStudio").Score;
        var nonContiguous = FuzzyMatcher.Match("bcn", "Beacon").Score;

        Assert.Multiple(() =>
        {
            Assert.That(exact, Is.GreaterThan(prefix));
            Assert.That(prefix, Is.GreaterThan(wordInitial));
            Assert.That(wordInitial, Is.GreaterThan(contiguous));
            Assert.That(contiguous, Is.GreaterThan(acronym));
            Assert.That(acronym, Is.GreaterThan(nonContiguous));
        });
    }

    [Test]
    public void VisualStudioCodeOutranksCamelCaseVideoStudioForVs() =>
        Assert.That(
            FuzzyMatcher.Match("vs", "Visual Studio Code").Score,
            Is.GreaterThan(FuzzyMatcher.Match("vs", "VideoStudio").Score));

    [Test]
    public void MatchRejectsBelowThreshold() =>
        Assert.That(FuzzyMatcher.Match("az", "a very very long candidate ending with z").Success, Is.False);
}
