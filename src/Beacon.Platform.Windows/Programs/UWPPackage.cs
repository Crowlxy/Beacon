// Adapted from Beacon-old/Plugins/Flow.Launcher.Plugin.Program/Programs/UWPPackage.cs (Flow Launcher/Wox, MIT).
using System.Runtime.InteropServices;

namespace Beacon.Platform.Windows.Programs;

public sealed record UWPPackage(string Name, string Path) : IProgram
{
    public static IReadOnlyList<UWPPackage> All()
    {
        var shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType is null) return [];
        object? shell = null;
        object? folder = null;
        object? items = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            folder = ((dynamic)shell!).NameSpace("shell:AppsFolder");
            items = ((dynamic)folder).Items();
            var apps = new List<UWPPackage>();
            foreach (var value in (dynamic)items)
            {
                object item = value;
                try
                {
                    var path = (string)((dynamic)item).Path;
                    var name = (string)((dynamic)item).Name;
                    if (path.Contains('!') && !string.IsNullOrWhiteSpace(name))
                        apps.Add(new(name, "shell:AppsFolder\\" + path));
                }
                finally
                {
                    if (Marshal.IsComObject(item)) Marshal.FinalReleaseComObject(item);
                }
            }
            return apps;
        }
        catch (Exception exception) when (exception is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            return [];
        }
        finally
        {
            if (items is not null && Marshal.IsComObject(items)) Marshal.FinalReleaseComObject(items);
            if (folder is not null && Marshal.IsComObject(folder)) Marshal.FinalReleaseComObject(folder);
            if (shell is not null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }
    }
}
