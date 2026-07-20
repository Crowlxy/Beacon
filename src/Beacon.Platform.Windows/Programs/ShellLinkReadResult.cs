// Adapted from Beacon-old/Plugins/Flow.Launcher.Plugin.Program/Programs/ShellLinkReadResult.cs (Flow Launcher/Wox, MIT).
namespace Beacon.Platform.Windows.Programs;

public readonly record struct ShellLinkReadResult(string TargetPath, string Description, string Arguments);
