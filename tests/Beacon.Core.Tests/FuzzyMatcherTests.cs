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
    public void MatchRejectsBelowThreshold() =>
        Assert.That(FuzzyMatcher.Match("az", "a very very long candidate ending with z").Success, Is.False);
}
