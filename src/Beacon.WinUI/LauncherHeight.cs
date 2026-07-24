namespace Beacon.WinUI;

public static class LauncherHeight
{
    /// <summary>
    /// ランチャーウィンドウの必要な高さ。
    /// <paramref name="scopeChipBlockHeight"/> と <paramref name="statusRowBlockHeight"/> は
    /// **要素の高さに自分の上下Marginを足した値**を渡す。Marginを足さないとGridのAuto行が要求する高さに届かず、
    /// 星形の結果行が先に潰れたうえで最後のStatusRowが下端で切れる。
    /// </summary>
    public static double Calculate(
        double searchBarHeight,
        double rowHeight,
        double listVerticalSpace,
        int visibleCount,
        int maximumResults,
        double scopeChipBlockHeight = 0,
        double statusRowBlockHeight = 0) =>
        searchBarHeight
        + scopeChipBlockHeight
        + (rowHeight * Math.Clamp(visibleCount, 0, maximumResults))
        + (visibleCount > 0 ? listVerticalSpace : 0)
        + statusRowBlockHeight;
}
