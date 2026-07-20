// Adapted from Beacon-old/Plugins/Flow.Launcher.Plugin.Program/Programs/IProgram.cs (Flow Launcher/Wox, MIT).
namespace Beacon.Platform.Windows.Programs;

public interface IProgram
{
    string Name { get; }
    string Path { get; }
}
