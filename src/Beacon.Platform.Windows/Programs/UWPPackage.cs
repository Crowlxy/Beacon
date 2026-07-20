// Adapted from Beacon-old/Plugins/Flow.Launcher.Plugin.Program/Programs/UWPPackage.cs (Flow Launcher/Wox, MIT).
namespace Beacon.Platform.Windows.Programs;

public sealed record UWPPackage(string Name, string Path) : IProgram;
