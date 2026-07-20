// Adapted from Beacon-old/Plugins/Flow.Launcher.Plugin.Explorer/Search/Everything/EverythingSearchOption.cs (Flow Launcher/Wox, MIT).
namespace Beacon.Platform.Windows.Everything;

public readonly record struct EverythingSearchOption(
    string Keyword,
    EverythingSortOption SortOption = EverythingSortOption.NameAscending,
    int Offset = 0,
    int MaxCount = 100,
    bool IsFullPathSearch = true);
