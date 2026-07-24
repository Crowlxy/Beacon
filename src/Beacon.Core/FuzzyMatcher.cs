// Adapted from Beacon-old/Flow.Launcher.Infrastructure/StringMatcher.cs and
// Flow.Launcher.Plugin/SharedModels/MatchResult.cs.
// Copyright (c) Flow Launcher and Wox contributors. Licensed under the MIT License.
namespace Beacon.Core;

public sealed record FuzzyMatchResult(bool Success, int Score, IReadOnlyList<int> MatchedIndices);

public static class FuzzyMatcher
{
    public const int RegularThreshold = 50;
    public const int ExactTier = 600;
    public const int PrefixTier = 300;
    public const int OtherTier = 150;
    private const int ExactRawScore = 140;
    private const int WordInitialRawScore = 150;
    private const int ContiguousRawScore = 100;
    private const int AcronymRawScore = 80;
    private static readonly FuzzyMatchResult NoMatch = new(false, 0, Array.Empty<int>());

    public static FuzzyMatchResult Match(string query, string candidate)
    {
        query = query.Trim();
        if (query.Length == 0 || candidate.Length == 0) return NoMatch;

        var normalizedQuery = DiacriticsNormalizer.Normalize(query);
        var normalizedCandidate = DiacriticsNormalizer.Normalize(candidate);
        var compactQuery = normalizedQuery.Where(character => !char.IsWhiteSpace(character)).ToArray();
        if (compactQuery.Length == 0) return NoMatch;

        if (normalizedCandidate.Equals(normalizedQuery, StringComparison.Ordinal))
            return MatchAt(ExactTier + ExactRawScore, 0, compactQuery.Length);

        var indices = MatchWordInitials(compactQuery, normalizedCandidate);
        var rawScore = indices.Count == compactQuery.Length
            ? BoundaryRawScore(WordInitialRawScore, normalizedCandidate, indices, IsWordStart)
            : 0;
        if (rawScore < RegularThreshold) rawScore = 0;
        if (rawScore == 0)
        {
            indices = MatchContiguous(compactQuery, normalizedCandidate);
            if (indices.Count == compactQuery.Length)
                rawScore = ContiguousRawScore + PositionAndLengthBonus(candidate, indices[0], compactQuery.Length);
        }
        if (rawScore == 0)
        {
            indices = MatchAcronym(compactQuery, candidate, normalizedCandidate);
            if (indices.Count == compactQuery.Length)
                rawScore = BoundaryRawScore(AcronymRawScore, candidate, indices, IsAcronym);
            if (rawScore < RegularThreshold) rawScore = 0;
        }
        if (rawScore == 0)
        {
            indices = MatchSubsequence(compactQuery, normalizedCandidate);
            if (indices.Count != compactQuery.Length) return NoMatch;
            var searchScore = CalculateSearchScore(candidate, compactQuery.Length, indices);
            if (searchScore < RegularThreshold) return NoMatch;
            rawScore = RegularThreshold + Math.Min(searchScore - RegularThreshold, 29);
        }

        var tier = normalizedCandidate.StartsWith(normalizedQuery, StringComparison.Ordinal)
            ? PrefixTier
            : OtherTier;
        return new(true, tier + rawScore, indices);
    }

    private static FuzzyMatchResult MatchAt(int score, int start, int length) =>
        new(true, score, Enumerable.Range(start, length).ToArray());

    private static List<int> MatchWordInitials(char[] query, string candidate)
    {
        var matches = new List<int>(query.Length);
        var queryIndex = 0;
        for (var index = 0; index < candidate.Length && queryIndex < query.Length; index++)
        {
            if ((index != 0 && !char.IsWhiteSpace(candidate[index - 1])) || candidate[index] != query[queryIndex]) continue;
            matches.Add(index);
            queryIndex++;
        }
        return matches;
    }

    private static List<int> MatchContiguous(char[] query, string candidate)
    {
        var compact = new string(query);
        var start = candidate.IndexOf(compact, StringComparison.Ordinal);
        return start < 0 ? [] : Enumerable.Range(start, query.Length).ToList();
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

    private static int BoundaryRawScore(
        int baseScore,
        string candidate,
        List<int> indices,
        Func<string, int, bool> isBoundary)
    {
        var boundaryCount = Enumerable.Range(0, candidate.Length).Count(index => isBoundary(candidate, index));
        return boundaryCount == 0 ? 0 : baseScore * indices.Count / boundaryCount + CompactnessBonus(indices);
    }
    private static int CompactnessBonus(List<int> indices)
    {
        if (indices.Count < 2) return 10;
        var span = indices[^1] - indices[0] + 1;
        return Math.Max(0, 20 - Math.Min(span - indices.Count, 20));
    }

    private static int PositionAndLengthBonus(string candidate, int start, int queryLength)
    {
        var positionBonus = Math.Max(0, 10 - Math.Min(start, 10));
        var lengthBonus = Math.Max(0, 10 - Math.Min(candidate.Length - queryLength, 10));
        return positionBonus + lengthBonus;
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

    private static bool IsWordStart(string value, int index) =>
        index == 0 || char.IsWhiteSpace(value[index - 1]);

    private static bool IsAcronym(string value, int index) =>
        IsWordStart(value, index) || char.IsUpper(value[index]) || char.IsDigit(value[index]);
}
