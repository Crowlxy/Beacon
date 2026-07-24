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
    [TestCase("1.2.3")]
    [TestCase("1...")]
    [TestCase("...")]
    public async Task CalculatorIgnoresTextAndMalformedExpressions(string query) =>
        Assert.That(await Results(new CalculatorSearchProvider(), query), Is.Empty);

    // 病的入力（深い括弧ネスト・^連鎖・連続する単項符号）は捕捉不能な StackOverflowException で
    // 常駐プロセスごと落とし得た（LESSONS.md 2026-07-24）。入力長上限＋再帰深さガードで
    // 「評価不能（結果なし）」に落ち、プロセスもテストランナーも生存することを固定する。
    [Test]
    public async Task CalculatorSurvivesPathologicalInput()
    {
        var provider = new CalculatorSearchProvider();
        string[] pathological =
        [
            new string('(', 5000) + "1" + new string(')', 5000),
            string.Join("^", Enumerable.Repeat("2", 5000)),
            new string('-', 5000) + "1",
        ];
        foreach (var query in pathological)
            Assert.That(await Results(provider, query), Is.Empty, $"query length {query.Length}");
    }

    // 上限内の正当な入れ子は引き続き評価できること（回帰防止）。
    [Test]
    public async Task CalculatorStillEvaluatesModestNesting() =>
        Assert.That((await Results(new CalculatorSearchProvider(), "(((1+2)))*2")).Single().Title, Is.EqualTo("6"));

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
            Assert.That(result.Subtitle, Is.EqualTo("Google"));
            Assert.That(result.ExecutionToken, Does.StartWith("https://www.google.com/search?"));
            Assert.That(result.Icon.Source, Is.EqualTo(IconSource.UriOrDataUri));
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
