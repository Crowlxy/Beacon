// Adapted from Beacon-old/Plugins/Flow.Launcher.Plugin.Program/Programs/ShellLinkReader.cs (Flow Launcher/Wox, MIT).
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace Beacon.Platform.Windows.Programs;

public static class ShellLinkReader
{
    public static ShellLinkReadResult Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        object link = new ShellLink();
        try
        {
            ((IPersistFile)link).Load(Path.GetFullPath(path), 0);
            var shellLink = (IShellLinkW)link;
            shellLink.Resolve(IntPtr.Zero, 1);
            var target = new StringBuilder(32768);
            var arguments = new StringBuilder(32768);
            var description = new StringBuilder(1024);
            shellLink.GetPath(target, target.Capacity, IntPtr.Zero, 4);
            shellLink.GetArguments(arguments, arguments.Capacity);
            shellLink.GetDescription(description, description.Capacity);
            return new(target.ToString(), description.ToString(), arguments.ToString());
        }
        catch (COMException)
        {
            return default;
        }
        finally { Marshal.FinalReleaseComObject(link); }
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private sealed class ShellLink;

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int maxPath, IntPtr findData, uint flags);
        void GetIDList(out IntPtr pidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description, int maxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int maxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int maxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCommand);
        void SetShowCmd(int showCommand);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int iconPathLength, out int iconIndex);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr hwnd, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
}
