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
        // 再帰下降パーサは括弧ネスト・単項符号・^連鎖で無制限に再帰し、病的入力で StackOverflowException を
        // 起こす。.NET の StackOverflowException は try/catch で捕捉できず常駐プロセスごと即死するため、
        // 入力長と再帰深さの両方で先回りして FormatException 経由の「評価不能（false）」に落とす。
        private const int MaxLength = 256;
        private const int MaxDepth = 128;
        private int _position;
        private int _depth;

        public static bool TryEvaluate(string text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text) || text.Length > MaxLength || !text.Any(char.IsDigit)) return false;
            try
            {
                var parser = new ExpressionParser(text);
                value = parser.Expression();
                parser.Space();
                return parser._position == text.Length && double.IsFinite(value);
            }
            catch (FormatException) { return false; }
            catch (DivideByZeroException) { return false; }
            catch (OverflowException) { return false; }
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
            var value = Power();
            while (true)
            {
                Space();
                if (Take('*')) value *= Power();
                else if (Take('/'))
                {
                    var divisor = Power();
                    if (divisor == 0) throw new DivideByZeroException();
                    value /= divisor;
                }
                else if (Take('%'))
                {
                    var divisor = Power();
                    if (divisor == 0) throw new DivideByZeroException();
                    value %= divisor;
                }
                else return value;
            }
        }

        private double Power()
        {
            var value = Factor();
            Space();
            return Take('^') ? Math.Pow(value, Power()) : value;
        }

        private double Factor()
        {
            // 括弧ネスト・単項符号・^連鎖のいずれの再帰も必ず Factor を1レベルにつき1回通るため、
            // ここで深さを数えれば全経路の再帰深さを一点で抑えられる。上限超過は FormatException にして
            // 既存の「評価不能（false）」経路へ乗せる。
            if (++_depth > MaxDepth) throw new FormatException();
            try
            {
                Space();
                if (Take('+')) return Factor();
                if (Take('-')) return -Factor();
                if (Take('('))
                {
                    var value = Expression();
                    Space();
                    if (!Take(')')) throw new FormatException();
                    return Percentage(value);
                }
                var start = _position;
                while (_position < text.Length && (char.IsDigit(text[_position]) || text[_position] == '.')) _position++;
                if (!double.TryParse(text[start.._position], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
                    throw new FormatException();
                return Percentage(number);
            }
            finally { _depth--; }
        }

        private double Percentage(double value)
        {
            var position = _position;
            Space();
            if (!Take('%')) return value;
            Space();
            if (_position == text.Length || ")+-*/^".Contains(text[_position])) return value / 100;
            _position = position;
            return value;
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

/// <summary>
/// 入力がURLとして解釈できるときに「ブラウザで開く」結果を出す。
/// アイコンは実際に開くブラウザのものを使いたいため、プラットフォーム層から差し込めるようにしている。
/// </summary>
public sealed class UrlSearchProvider(IconDescriptor? browserIcon = null) : ISearchProvider
{
    public const string Id = "builtin.url";
    private static readonly IconDescriptor FallbackIcon = new(IconSource.FluentGlyph, "\uE774");
    private readonly IconDescriptor _icon = browserIcon ?? FallbackIcon;
    public string ProviderId => Id;

    public async IAsyncEnumerable<SearchResultDto> SearchAsync(SearchRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (request.Scope is not (QueryScope.All or QueryScope.Url) || !TryCreateUri(request.RawQuery, out var uri)) yield break;
        cancellationToken.ThrowIfCancellationRequested();
        yield return new SearchResultDto
        {
            Id = $"url:{uri.AbsoluteUri}", ProviderId = Id, Title = uri.AbsoluteUri,
            Subtitle = "Open in browser", Kind = ResultKind.Url, Score = 100,
            Icon = _icon, ExecutionToken = uri.AbsoluteUri,
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
        var uri = "https://www.google.com/search?q=" + Uri.EscapeDataString(request.RawQuery.Trim());
        yield return new SearchResultDto
        {
            Id = $"web:{request.RawQuery}", ProviderId = Id, Title = $"Search the web for “{request.RawQuery.Trim()}”",
            Subtitle = "Google", Kind = ResultKind.WebSearch, Score = FallbackScore,
            Icon = new(IconSource.UriOrDataUri, "https://www.google.com/favicon.ico"), ExecutionToken = uri,
        };
        await Task.CompletedTask;
    }
}
