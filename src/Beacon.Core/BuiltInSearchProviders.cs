using System.Globalization;
using System.Runtime.CompilerServices;
using Beacon.Contracts;

namespace Beacon.Core;

public sealed class CalculatorSearchProvider : ISearchProvider
{
    public const string Id = "builtin.calculator";
    public string ProviderId => Id;

    public async IAsyncEnumerable<SearchResultDto> SearchAsync(
        SearchRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (request.Scope is not (QueryScope.All or QueryScope.Calculator) ||
            !ExpressionParser.TryEvaluate(request.RawQuery, out var value)) yield break;
        cancellationToken.ThrowIfCancellationRequested();
        var text = value.ToString("G15", CultureInfo.InvariantCulture);
        yield return new SearchResultDto
        {
            Id = $"calculation:{request.RawQuery}", ProviderId = Id, Title = text,
            Subtitle = "Copy result", Kind = ResultKind.Calculation, Score = 500,
            Icon = new(IconSource.FluentGlyph, "\uE8EF"), ExecutionToken = text, CopyText = text,
        };
        await Task.CompletedTask;
    }

    private sealed class ExpressionParser(string text)
    {
        private int _position;

        public static bool TryEvaluate(string text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text) || !text.Any(char.IsDigit)) return false;
            try
            {
                var parser = new ExpressionParser(text);
                value = parser.Expression();
                parser.Space();
                return parser._position == text.Length && double.IsFinite(value);
            }
            catch (FormatException) { return false; }
            catch (DivideByZeroException) { return false; }
        }

        private double Expression()
        {
            var value = Term();
            while (true)
            {
                Space();
                if (Take('+')) value += Term();
                else if (Take('-')) value -= Term();
                else return value;
            }
        }

        private double Term()
        {
            var value = Factor();
            while (true)
            {
                Space();
                if (Take('*')) value *= Factor();
                else if (Take('/'))
                {
                    var divisor = Factor();
                    if (divisor == 0) throw new DivideByZeroException();
                    value /= divisor;
                }
                else return value;
            }
        }

        private double Factor()
        {
            Space();
            if (Take('+')) return Factor();
            if (Take('-')) return -Factor();
            if (Take('('))
            {
                var value = Expression();
                Space();
                if (!Take(')')) throw new FormatException();
                return value;
            }
            var start = _position;
            while (_position < text.Length && (char.IsDigit(text[_position]) || text[_position] == '.')) _position++;
            if (!double.TryParse(text[start.._position], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
                throw new FormatException();
            return number;
        }

        private bool Take(char value)
        {
            if (_position >= text.Length || text[_position] != value) return false;
            _position++;
            return true;
        }

        private void Space() { while (_position < text.Length && char.IsWhiteSpace(text[_position])) _position++; }
    }
}

public sealed class UrlSearchProvider : ISearchProvider
{
    public const string Id = "builtin.url";
    public string ProviderId => Id;

    public async IAsyncEnumerable<SearchResultDto> SearchAsync(SearchRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (request.Scope is not (QueryScope.All or QueryScope.Url) || !TryCreateUri(request.RawQuery, out var uri)) yield break;
        cancellationToken.ThrowIfCancellationRequested();
        yield return new SearchResultDto
        {
            Id = $"url:{uri.AbsoluteUri}", ProviderId = Id, Title = uri.AbsoluteUri,
            Subtitle = "Open in browser", Kind = ResultKind.Url, Score = 100,
            Icon = new(IconSource.FluentGlyph, "\uE774"), ExecutionToken = uri.AbsoluteUri,
        };
        await Task.CompletedTask;
    }

    public static bool TryCreateUri(string text, out Uri uri)
    {
        uri = null!;
        var candidate = text.Trim();
        if (candidate.Any(char.IsWhiteSpace)) return false;
        var explicitHttp = candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        if (!explicitHttp)
        {
            if (!candidate.StartsWith("www.", StringComparison.OrdinalIgnoreCase) &&
                (!candidate.Contains('.') || candidate.StartsWith('.') || candidate.EndsWith('.'))) return false;
            candidate = "https://" + candidate;
        }
        return Uri.TryCreate(candidate, UriKind.Absolute, out uri!) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
            (explicitHttp || uri.Host.Contains('.'));
    }
}

public sealed class WebSearchProvider : ISearchProvider
{
    public const string Id = "builtin.web";
    public const double FallbackScore = -250;
    public string ProviderId => Id;

    public async IAsyncEnumerable<SearchResultDto> SearchAsync(SearchRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (request.Scope is not (QueryScope.All or QueryScope.WebSearch) || string.IsNullOrWhiteSpace(request.RawQuery)) yield break;
        cancellationToken.ThrowIfCancellationRequested();
        var uri = "https://www.bing.com/search?q=" + Uri.EscapeDataString(request.RawQuery.Trim());
        yield return new SearchResultDto
        {
            Id = $"web:{request.RawQuery}", ProviderId = Id, Title = $"Search the web for “{request.RawQuery.Trim()}”",
            Subtitle = "Bing", Kind = ResultKind.WebSearch, Score = FallbackScore,
            Icon = new(IconSource.FluentGlyph, "\uE721"), ExecutionToken = uri,
        };
        await Task.CompletedTask;
    }
}
