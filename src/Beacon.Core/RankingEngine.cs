using Beacon.Contracts;

namespace Beacon.Core;

public sealed record RankingContext(
    DateTimeOffset Now,
    string? ActiveProcessName = null,
    string? ActiveFolder = null,
    bool PersonalizationEnabled = true);

public static class RankingEngine
{
    public const int RecentDayBoost = 120;
    public const int RecentWeekBoost = 60;
    public const int MaximumSelectionBoost = 180;
    public const int ContextBoost = 80;
    public const double SubtitleMatchScale = 0.65;
    public const double PathMatchScale = 0.5;
    public const int WebSearchPenalty = 250;

    public static double Score(
        SearchResultDto result,
        string query,
        UsageHistoryEntry? usage,
        int maximumSelectionCount,
        RankingContext context)
    {
        // Provider score and fuzzy quality are additive. Fuzzy tiers establish match intent,
        // while raw quality and boosts remain large enough for usage history to reorder close matches.
        var value = result.Score + BestTextMatchScore(result, query);

        if (context.PersonalizationEnabled && usage is not null)
        {
            var age = context.Now - usage.LastSelectedAt;
            if (age <= TimeSpan.FromHours(24)) value += RecentDayBoost;
            else if (age <= TimeSpan.FromDays(7)) value += RecentWeekBoost;
            if (maximumSelectionCount > 0)
                value += MaximumSelectionBoost * (double)usage.SelectionCount / maximumSelectionCount;
            if (!string.IsNullOrWhiteSpace(context.ActiveProcessName) &&
                string.Equals(usage.ActiveProcessName, context.ActiveProcessName, StringComparison.OrdinalIgnoreCase)) value += ContextBoost;
            if (!string.IsNullOrWhiteSpace(context.ActiveFolder) && !string.IsNullOrWhiteSpace(result.FilePath) &&
                IsWithin(result.FilePath, context.ActiveFolder)) value += ContextBoost;
        }

        if (result.Kind == ResultKind.WebSearch && result.Score > -WebSearchPenalty) value -= WebSearchPenalty;
        return value;
    }

    public static double BestTextMatchScore(SearchResultDto result, string query)
    {
        var title = FuzzyMatcher.Match(query, result.Title).Score;
        var subtitle = string.IsNullOrWhiteSpace(result.Subtitle)
            ? 0
            : FuzzyMatcher.Match(query, result.Subtitle).Score * SubtitleMatchScale;
        var path = string.IsNullOrWhiteSpace(result.FilePath)
            ? 0
            : FuzzyMatcher.Match(query, result.FilePath).Score * PathMatchScale;
        return Math.Max(title, Math.Max(subtitle, path));
    }

    private static bool IsWithin(string path, string folder)
    {
        try
        {
            var relative = Path.GetRelativePath(Path.GetFullPath(folder), Path.GetFullPath(path));
            return relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
