// Adapted from Beacon-old/Flow.Launcher.Infrastructure/StringMatcher.cs and
// Flow.Launcher.Plugin/SharedModels/MatchResult.cs.
// Copyright (c) Flow Launcher and Wox contributors. Licensed under the MIT License.
namespace Beacon.Core;

public sealed record FuzzyMatchResult(bool Success, int Score, IReadOnlyList<int> MatchedIndices);

public static class FuzzyMatcher
{
    public const int RegularThreshold = 50;
    private static readonly FuzzyMatchResult NoMatch = new(false, 0, Array.Empty<int>());

    public static FuzzyMatchResult Match(string query, string candidate)
    {
        query = query.Trim();
        if (query.Length == 0 || candidate.Length == 0) return NoMatch;

        var normalizedQuery = DiacriticsNormalizer.Normalize(query);
        var normalizedCandidate = DiacriticsNormalizer.Normalize(candidate);
        var compactQuery = normalizedQuery.Where(character => !char.IsWhiteSpace(character)).ToArray();
        if (compactQuery.Length == 0) return NoMatch;

        var indices = MatchAcronym(compactQuery, candidate, normalizedCandidate);
        var rawScore = indices.Count == compactQuery.Length
            ? AcronymRawScore(candidate, indices.Count)
            : 0;
        if (rawScore < RegularThreshold)
        {
            indices = MatchSubsequence(compactQuery, normalizedCandidate);
            if (indices.Count != compactQuery.Length) return NoMatch;
            rawScore = CalculateSearchScore(candidate, compactQuery.Length, indices);
        }
        if (rawScore < RegularThreshold) return NoMatch;

        var tier = normalizedCandidate.Equals(normalizedQuery, StringComparison.Ordinal) ? 600
            : normalizedCandidate.StartsWith(normalizedQuery, StringComparison.Ordinal) ? 300
            : 150;
        return new(true, tier, indices);
    }

    private static List<int> MatchAcronym(char[] query, string candidate, string normalizedCandidate)
    {
        var matches = new List<int>(query.Length);
        var queryIndex = 0;
        for (var index = 0; index < candidate.Length && queryIndex < query.Length; index++)
        {
            if (!IsAcronym(candidate, index) || normalizedCandidate[index] != query[queryIndex]) continue;
            matches.Add(index);
            queryIndex++;
        }
        return matches;
    }

    private static List<int> MatchSubsequence(char[] query, string candidate)
    {
        var matches = new List<int>(query.Length);
        var searchAt = 0;
        foreach (var character in query)
        {
            var found = candidate.IndexOf(character, searchAt);
            if (found < 0) return [];
            matches.Add(found);
            searchAt = found + 1;
        }
        return matches;
    }

    private static int AcronymRawScore(string candidate, int matchCount)
    {
        var acronymCount = Enumerable.Range(0, candidate.Length).Count(index => IsAcronymCount(candidate, index));
        return acronymCount == 0 ? 0 : matchCount * 100 / acronymCount;
    }

    private static int CalculateSearchScore(string candidate, int queryLength, List<int> indices)
    {
        var first = indices[0];
        var span = indices[^1] - first + 1;
        var score = 100 * (queryLength + 1) / (first + span + 2);
        var difference = candidate.Length - queryLength;
        if (difference < 5) score += 20;
        else if (difference < 10) score += 10;
        if (indices.Zip(indices.Skip(1), (left, right) => right == left + 1).All(value => value))
            score += Math.Min(queryLength, 4) * 10 + Math.Max(queryLength - 4, 0) * 5;
        return score;
    }

    private static bool IsAcronym(string value, int index) =>
        index == 0 || char.IsWhiteSpace(value[index - 1]) || char.IsUpper(value[index]) || char.IsDigit(value[index]);

    private static bool IsAcronymCount(string value, int index) =>
        index == 0 || char.IsWhiteSpace(value[index - 1]) || char.IsUpper(value[index]) ||
        char.IsDigit(value[index]) && (index == 0 || !char.IsDigit(value[index - 1]));
}
