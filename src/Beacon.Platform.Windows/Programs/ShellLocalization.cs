// Adapted from Beacon-old/Plugins/Flow.Launcher.Plugin.Program/Programs/ShellLocalization.cs (Flow Launcher/Wox, MIT).
namespace Beacon.Platform.Windows.Programs;

public static class ShellLocalization
{
    public static string DisplayName(string shortcutPath) => System.IO.Path.GetFileNameWithoutExtension(shortcutPath);
}
