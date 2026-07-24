using Beacon.Contracts;

namespace Beacon.Core;

/// <summary>
/// stable-prefix / append-only 方式の純関数。一度表示した上位行の相対順序（committedIds）を凍結し、
/// 未committedの候補はスコア降順（Id昇順でタイブレーク）で末尾へ追記するだけで、既存の表示行を
/// 並び替えない（reshuffle回避）。副作用なし・UI型非依存。
/// </summary>
public static class ResultMerger
{
    /// <param name="committedIds">これまで表示に出したidの凍結順。</param>
    /// <param name="candidates">id→DTOの当該クエリで到着済み全件。</param>
    /// <param name="max">最大表示数。</param>
    /// <returns>表示すべきDTOを順序どおり（committed順の生存分を先頭固定、残りをScore降順→Id昇順で追記、maxで切る）。</returns>
    public static IReadOnlyList<SearchResultDto> Merge(
        IReadOnlyList<string> committedIds,
        IReadOnlyDictionary<string, SearchResultDto> candidates,
        int max)
    {
        var committedSet = new HashSet<string>(committedIds, StringComparer.Ordinal);
        var appended = candidates.Values
            .Where(r => !committedSet.Contains(r.Id))
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Id, StringComparer.Ordinal);
        // committedのidがcandidatesから消えている場合（本来起きないが防御的に）は生存分のみ採用する。
        return committedIds
            .Where(candidates.ContainsKey)
            .Select(id => candidates[id])
            .Concat(appended)
            .Take(max)
            .ToArray();
    }
}
