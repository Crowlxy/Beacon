// Adapted by service from Beacon-old/Flow.Launcher.Infrastructure/Win32Helper.cs (Flow Launcher/Wox, MIT).
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Beacon.Contracts;
using Windows.Win32;

namespace Beacon.Platform.Windows;

public static class ProcessLaunchService
{
    public static Process? Start(string fileName, string? arguments = null) =>
        Process.Start(new ProcessStartInfo(fileName, arguments ?? string.Empty) { UseShellExecute = true });
}

public static class FileOperationService
{
    public static Process? Open(string path) => ProcessLaunchService.Start(path);
    public static Process? ShowInFolder(string path) =>
        ProcessLaunchService.Start("explorer.exe", $"/select,\"{Path.GetFullPath(path)}\"");
}

public static class ActiveWindowService
{
    public static unsafe nint GetHandle() => (nint)PInvoke.GetForegroundWindow().Value;

    public static uint GetProcessId()
    {
        _ = PInvoke.GetWindowThreadProcessId(PInvoke.GetForegroundWindow(), out var processId);
        return processId;
    }

    public static string? GetProcessName()
    {
        try
        {
            var processId = GetProcessId();
            return processId == 0 ? null : Process.GetProcessById((int)processId).ProcessName;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}

public static class ExplorerPathService
{
    public static string? GetCurrentPath()
    {
        var shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType is null) return null;
        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            foreach (var window in shell.Windows())
            {
                try
                {
                    var path = (string?)window.Document?.Folder?.Self?.Path;
                    if (!string.IsNullOrWhiteSpace(path)) return path;
                }
                catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { }
            }
            return null;
        }
        finally { Marshal.FinalReleaseComObject(shell); }
    }
}

public static partial class ClipboardTextService
{
    private const uint UnicodeText = 13;

    public static string? Get()
    {
        if (!OpenClipboard(IntPtr.Zero)) return null;
        try
        {
            var handle = GetClipboardData(UnicodeText);
            if (handle == IntPtr.Zero) return null;
            var pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero) return null;
            try { return Marshal.PtrToStringUni(pointer); }
            finally { _ = GlobalUnlock(handle); }
        }
        finally { _ = CloseClipboard(); }
    }

    public static void Set(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!OpenClipboard(IntPtr.Zero)) throw new InvalidOperationException("Clipboard is busy.");
        try
        {
            if (!EmptyClipboard()) throw new InvalidOperationException("Clipboard could not be cleared.");
            var bytes = Encoding.Unicode.GetBytes(text + '\0');
            var handle = GlobalAlloc(0x0002, (nuint)bytes.Length);
            if (handle == IntPtr.Zero) throw new InvalidOperationException("Clipboard memory allocation failed.");
            var pointer = GlobalLock(handle);
            try { Marshal.Copy(bytes, 0, pointer, bytes.Length); }
            finally { _ = GlobalUnlock(handle); }
            if (SetClipboardData(UnicodeText, handle) == IntPtr.Zero) _ = GlobalFree(handle);
        }
        finally { _ = CloseClipboard(); }
    }

    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool OpenClipboard(IntPtr owner);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool CloseClipboard();
    [LibraryImport("user32.dll")] private static partial IntPtr GetClipboardData(uint format);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool EmptyClipboard();
    [LibraryImport("user32.dll")] private static partial IntPtr SetClipboardData(uint format, IntPtr memory);
    [LibraryImport("kernel32.dll")] private static partial IntPtr GlobalAlloc(uint flags, nuint bytes);
    [LibraryImport("kernel32.dll")] private static partial IntPtr GlobalLock(IntPtr memory);
    [LibraryImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool GlobalUnlock(IntPtr memory);
    [LibraryImport("kernel32.dll")] private static partial IntPtr GlobalFree(IntPtr memory);
}

public static class ShellImageService
{
    public static IconDescriptor Icon(string path) => new(IconSource.FileShellIcon, Path.GetFullPath(path));
    public static IconDescriptor Thumbnail(string path) => new(IconSource.FileThumbnail, Path.GetFullPath(path));
}
