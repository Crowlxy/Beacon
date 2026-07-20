// Adapted from Beacon-old/Plugins/Flow.Launcher.Plugin.Explorer/Search/Everything/EverythingApiDllImport.cs (Flow Launcher/Wox, MIT).
using System.Runtime.InteropServices;

namespace Beacon.Platform.Windows.Everything;

internal static partial class EverythingApiDllImport
{
    private const string Dll = "Everything.dll";

    [LibraryImport(Dll, EntryPoint = "Everything_SetSearchW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int SetSearch(string search);
    [LibraryImport(Dll, EntryPoint = "Everything_SetMatchPath")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetMatchPath([MarshalAs(UnmanagedType.Bool)] bool enable);
    [LibraryImport(Dll, EntryPoint = "Everything_SetMax")]
    internal static partial void SetMax(int max);
    [LibraryImport(Dll, EntryPoint = "Everything_SetOffset")]
    internal static partial void SetOffset(int offset);
    [LibraryImport(Dll, EntryPoint = "Everything_SetSort")]
    internal static partial void SetSort(EverythingSortOption sort);
    [LibraryImport(Dll, EntryPoint = "Everything_QueryW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool Query([MarshalAs(UnmanagedType.Bool)] bool wait);
    [LibraryImport(Dll, EntryPoint = "Everything_GetNumResults")]
    internal static partial int GetNumResults();
    [LibraryImport(Dll, EntryPoint = "Everything_IsFolderResult")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsFolderResult(int index);
    [LibraryImport(Dll, EntryPoint = "Everything_GetResultFullPathNameW")]
    internal static unsafe partial void GetResultFullPathName(int index, char* buffer, int maxCount);
    [LibraryImport(Dll, EntryPoint = "Everything_GetLastError")]
    internal static partial EverythingApi.StateCode GetLastError();
    [LibraryImport(Dll, EntryPoint = "Everything_GetMajorVersion")]
    internal static partial int GetMajorVersion();
    [LibraryImport(Dll, EntryPoint = "Everything_Reset")]
    internal static partial void Reset();
}
