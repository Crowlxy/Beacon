// Adapted from Beacon-old/Plugins/Flow.Launcher.Plugin.Program/Programs/Win32.cs (Flow Launcher/Wox, MIT).
namespace Beacon.Platform.Windows.Programs;

public sealed record Win32(string Name, string Path) : IProgram;
