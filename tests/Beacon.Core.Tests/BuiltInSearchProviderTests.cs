using Beacon.Contracts;
using Beacon.Core;
using NUnit.Framework;

namespace Beacon.Core.Tests;

public sealed class BuiltInSearchProviderTests
{
    [TestCase("1+2*3", "7")]
    [TestCase("(1.5 + 2.5) / 2", "2")]
    [TestCase("-3 + 5", "2")]
    [TestCase("2^3^2", "512")]
    [TestCase("10%3", "1")]
    [TestCase("50%", "0.5")]
    public async Task CalculatorEvaluatesRepresentativeExpressions(string query, string expected) =>
        Assert.That((await Results(new CalculatorSearchProvider(), query)).Single().Title, Is.EqualTo(expected));

    [TestCase("hello")]
    [TestCase("1+")]
    [TestCase("1/0")]
    public async Task CalculatorIgnoresTextAndMalformedExpressions(string query) =>
        Assert.That(await Results(new CalculatorSearchProvider(), query), Is.Empty);

    [TestCase("https://example.com")]
    [TestCase("http://example.com/a")]
    [TestCase("http://localhost")]
    [TestCase("www.example.com")]
    [TestCase("example.com")]
    public void UrlAcceptsExpectedForms(string query) => Assert.That(UrlSearchProvider.TryCreateUri(query, out _), Is.True);

    [TestCase("hello world")]
    [TestCase("localhost")]
    [TestCase("ftp://example.com")]
    [TestCase(".example")]
    public void UrlRejectsNonUrls(string query) => Assert.That(UrlSearchProvider.TryCreateUri(query, out _), Is.False);

    [Test]
    public async Task WebFallbackAlwaysReturnsOneLowestScoredResult()
    {
        var result = (await Results(new WebSearchProvider(), "beacon launcher")).Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.Kind, Is.EqualTo(ResultKind.WebSearch));
            Assert.That(result.Score, Is.EqualTo(WebSearchProvider.FallbackScore));
            Assert.That(result.Score, Is.LessThan(0));
        });
    }

    private static async Task<List<SearchResultDto>> Results(ISearchProvider provider, string query)
    {
        var results = new List<SearchResultDto>();
        await foreach (var result in provider.SearchAsync(new("session", query, QueryScope.All, ContractVersion.Current), CancellationToken.None))
            results.Add(result);
        return results;
    }
}
