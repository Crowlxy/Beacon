using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Beacon.Core;

namespace Beacon.Platform.Windows;

public sealed class ClipboardHistoryService : IDisposable
{
    private readonly IntPtr _windowHandle;
    private readonly string _path;
    private readonly Action<string>? _log;
    private ClipboardHistory _history;
    public bool Enabled { get; private set; }
    public IReadOnlyCollection<string> ExcludedApplications { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ClipboardHistoryItem> Items => _history.Items;

    public ClipboardHistoryService(IntPtr windowHandle, string dataRoot, Action<string>? log = null)
    {
        _windowHandle = windowHandle;
        _path = Path.Combine(dataRoot, "Clipboard", "history.dat");
        _log = log;
        _history = Load();
    }

    public void SetEnabled(bool enabled)
    {
        if (Enabled == enabled) return;
        if (enabled && !AddClipboardFormatListener(_windowHandle)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        if (!enabled) _ = RemoveClipboardFormatListener(_windowHandle);
        Enabled = enabled;
    }

    public void OnClipboardUpdate()
    {
        if (!Enabled || ClipboardReader.IsOwnerExcluded(ExcludedApplications) || !ClipboardReader.TryRead(_windowHandle, out var kind, out var content)) return;
        if (_history.Add(kind, content)) Save();
    }

    public void Delete(string id) { if (_history.Delete(id)) Save(); }
    public void Clear() { _history.Clear(); Save(); }
    public void Dispose() => SetEnabled(false);

    private ClipboardHistory Load()
    {
        try
        {
            if (!File.Exists(_path)) return new();
            var json = Encoding.UTF8.GetString(Dpapi.Unprotect(File.ReadAllBytes(_path)));
            return new(JsonSerializer.Deserialize<ClipboardHistoryItem[]>(json));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException or JsonException)
        {
            _log?.Invoke($"WARN Clipboard history read failed: {exception.Message}");
            return new();
        }
    }

    private void Save()
    {
        try
        {
            var encrypted = Dpapi.Protect(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_history.Items)));
            var temporary = _path + ".tmp";
            File.WriteAllBytes(temporary, encrypted);
            File.Move(temporary, _path, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            _log?.Invoke($"WARN Clipboard history write failed: {exception.Message}");
        }
    }

    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AddClipboardFormatListener(IntPtr windowHandle);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RemoveClipboardFormatListener(IntPtr windowHandle);
}

internal static partial class ClipboardReader
{
    private const uint UnicodeText = 13;
    private const uint FileDrop = 15;

    public static bool TryRead(IntPtr owner, out ClipboardContentKind kind, out string content)
    {
        kind = ClipboardContentKind.Text;
        content = string.Empty;
        var excluded = RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
        if (excluded != 0 && IsClipboardFormatAvailable(excluded)) return false;
        if (!OpenClipboard(owner)) return false;
        try
        {
            if (IsClipboardFormatAvailable(FileDrop) && ReadFiles(out content)) { kind = ClipboardContentKind.Files; return true; }
            var html = RegisterClipboardFormat("HTML Format");
            if (html != 0 && IsClipboardFormatAvailable(html) && ReadUtf8(html, out content)) { kind = ClipboardContentKind.Html; return true; }
            if (!ReadUnicode(UnicodeText, out content)) return false;
            kind = Uri.TryCreate(content, UriKind.Absolute, out _) ? ClipboardContentKind.Url : ClipboardContentKind.Text;
            return true;
        }
        finally { _ = CloseClipboard(); }
    }

    internal static bool IsOwnerExcluded(IEnumerable<string> exclusions)
    {
        var owner = GetClipboardOwner();
        if (owner == IntPtr.Zero) return false;
        _ = GetWindowThreadProcessId(owner, out var processId);
        if (processId == 0) return false;
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById((int)processId);
            return MatchesExcludedApplication(exclusions, process.ProcessName);
        }
        catch (ArgumentException) { return false; }
    }

    internal static bool MatchesExcludedApplication(IEnumerable<string> exclusions, string processName)
        => exclusions.Select(x => Path.GetFileNameWithoutExtension(x.Trim()))
            .Any(x => x.Equals(processName, StringComparison.OrdinalIgnoreCase));

    private static bool ReadUnicode(uint format, out string content)
    {
        content = string.Empty;
        var pointer = Lock(format, out var handle);
        if (pointer == IntPtr.Zero) return false;
        try { content = Marshal.PtrToStringUni(pointer) ?? string.Empty; return content.Length > 0; }
        finally { _ = GlobalUnlock(handle); }
    }

    private static bool ReadUtf8(uint format, out string content)
    {
        content = string.Empty;
        var pointer = Lock(format, out var handle);
        if (pointer == IntPtr.Zero) return false;
        try
        {
            var length = checked((int)GlobalSize(handle));
            var bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            content = Encoding.UTF8.GetString(bytes).TrimEnd('\0');
            return content.Length > 0;
        }
        finally { _ = GlobalUnlock(handle); }
    }

    private static unsafe bool ReadFiles(out string content)
    {
        content = string.Empty;
        var handle = GetClipboardData(FileDrop);
        if (handle == IntPtr.Zero) return false;
        var count = DragQueryFile(handle, uint.MaxValue, null, 0);
        var paths = new List<string>((int)count);
        var buffer = stackalloc char[32768];
        for (uint index = 0; index < count; index++)
        {
            var length = DragQueryFile(handle, index, buffer, 32768);
            paths.Add(new string(buffer, 0, checked((int)length)));
        }
        content = string.Join(Environment.NewLine, paths);
        return paths.Count > 0;
    }

    private static IntPtr Lock(uint format, out IntPtr handle)
    {
        handle = GetClipboardData(format);
        return handle == IntPtr.Zero ? IntPtr.Zero : GlobalLock(handle);
    }

    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool OpenClipboard(IntPtr owner);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint format);
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardOwner();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterClipboardFormat(string format);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr memory);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalUnlock(IntPtr memory);
    [DllImport("kernel32.dll")] private static extern nuint GlobalSize(IntPtr memory);
    [LibraryImport("shell32.dll", EntryPoint = "DragQueryFileW")] private static unsafe partial uint DragQueryFile(IntPtr drop, uint file, char* path, int length);
}

internal static class Dpapi
{
    public static byte[] Protect(byte[] data) => Transform(data, protect: true);
    public static byte[] Unprotect(byte[] data) => Transform(data, protect: false);

    private static byte[] Transform(byte[] data, bool protect)
    {
        var inputPointer = Marshal.AllocHGlobal(data.Length);
        try
        {
            Marshal.Copy(data, 0, inputPointer, data.Length);
            var input = new Blob(data.Length, inputPointer);
            var success = protect
                ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out var output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out output);
            if (!success) throw new CryptographicException(Marshal.GetLastWin32Error());
            try
            {
                var result = new byte[output.Length];
                Marshal.Copy(output.Data, result, 0, result.Length);
                return result;
            }
            finally { _ = LocalFree(output.Data); }
        }
        finally { Marshal.FreeHGlobal(inputPointer); }
    }

    [StructLayout(LayoutKind.Sequential)] private struct Blob(int length, IntPtr data) { public int Length = length; public IntPtr Data = data; }
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptProtectData(ref Blob input, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
}
