// Adapted from Beacon-old/Plugins/Flow.Launcher.Plugin.Explorer/Search/Everything/EverythingSortOption.cs (Flow Launcher/Wox, MIT).
namespace Beacon.Platform.Windows.Everything;

public enum EverythingSortOption : uint
{
    NameAscending = 1,
    NameDescending = 2,
    PathAscending = 3,
    PathDescending = 4,
    SizeAscending = 5,
    SizeDescending = 6,
    DateModifiedAscending = 13,
    DateModifiedDescending = 14,
    RunCountDescending = 20,
}
