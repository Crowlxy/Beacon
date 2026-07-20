namespace Beacon.WinUI;

public static class LauncherHeight
{
    public static double Calculate(double searchBarHeight, double rowHeight, double listVerticalSpace, int visibleCount, int maximumResults) =>
        searchBarHeight + (rowHeight * Math.Clamp(visibleCount, 0, maximumResults)) + (visibleCount > 0 ? listVerticalSpace : 0);
}
