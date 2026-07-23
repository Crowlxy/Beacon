using Beacon.Contracts;

namespace Beacon.Core;

public sealed record RankingContext(
    DateTimeOffset Now,
    string? ActiveProcessName = null,
    string? ActiveFolder = null,
    bool PersonalizationEnabled = true);

public static class RankingEngine
{
    public static double Score(
        SearchResultDto result,
        string query,
        UsageHistoryEntry? usage,
        int maximumSelectionCount,
        RankingContext context)
    {
        var value = result.Score;
        value += FuzzyMatcher.Match(query, result.Title).Score;

        if (context.PersonalizationEnabled && usage is not null)
        {
            var age = context.Now - usage.LastSelectedAt;
            if (age <= TimeSpan.FromHours(24)) value += 120;
            else if (age <= TimeSpan.FromDays(7)) value += 60;
            if (maximumSelectionCount > 0)
                value += 180d * usage.SelectionCount / maximumSelectionCount;
            if (!string.IsNullOrWhiteSpace(context.ActiveProcessName) &&
                string.Equals(usage.ActiveProcessName, context.ActiveProcessName, StringComparison.OrdinalIgnoreCase)) value += 80;
            if (!string.IsNullOrWhiteSpace(context.ActiveFolder) && !string.IsNullOrWhiteSpace(result.FilePath) &&
                IsWithin(result.FilePath, context.ActiveFolder)) value += 80;
        }

        if (result.Kind == ResultKind.WebSearch && result.Score > -250) value -= 250;
        return value;
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
