// Adapted from Beacon-old/Plugins/Flow.Launcher.Plugin.Program/Programs/ShellLocalization.cs (Flow Launcher/Wox, MIT).
using System.Runtime.InteropServices;

namespace Beacon.Platform.Windows.Programs;

public static class ShellLocalization
{
    public static unsafe string DisplayName(string shortcutPath)
    {
        var id = typeof(IShellItem).GUID;
        if (SHCreateItemFromParsingName(shortcutPath, IntPtr.Zero, ref id, out var item) != 0)
            return Path.GetFileNameWithoutExtension(shortcutPath);
        try
        {
            Marshal.ThrowExceptionForHR(item.GetDisplayName(0, out var displayName));
            try { return Marshal.PtrToStringUni(displayName) ?? Path.GetFileNameWithoutExtension(shortcutPath); }
            finally { Marshal.FreeCoTaskMem(displayName); }
        }
        catch (COMException)
        {
            return Path.GetFileNameWithoutExtension(shortcutPath);
        }
        finally
        {
            Marshal.FinalReleaseComObject(item);
        }
    }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        [PreserveSig] int BindToHandler(IntPtr bindContext, ref Guid handlerId, ref Guid interfaceId, out IntPtr value);
        [PreserveSig] int GetParent(out IShellItem parent);
        [PreserveSig] int GetDisplayName(uint displayNameType, out IntPtr displayName);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid id, out IShellItem item);
}
