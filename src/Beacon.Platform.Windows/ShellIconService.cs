using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Beacon.Platform.Windows;

public sealed record ShellIconPixels(int Width, int Height, byte[] BgraPixels);

public static class ShellIconService
{
    private const uint IconOnly = 0x00000004;
    private const uint ThumbnailOnly = 0x00000008;

    public static ShellIconPixels GetIcon(string path, int size = 32) =>
        GetImage(path, size, Directory.Exists(path) ? IconOnly : 0);

    public static ShellIconPixels GetThumbnail(string path, int size = 32)
    {
        try { return GetImage(path, size, ThumbnailOnly); }
        catch (COMException) { return GetImage(path, size, IconOnly); }
    }

    private static ShellIconPixels GetImage(string path, int size, uint flags)
    {
        var id = typeof(IShellItemImageFactory).GUID;
        var shellPath = path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) ? path : Path.GetFullPath(path);
        Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(shellPath, IntPtr.Zero, ref id, out var factory));
        try
        {
            var requested = new Size { Width = size, Height = size };
            Marshal.ThrowExceptionForHR(factory.GetImage(requested, flags, out var bitmap));
            try { return ReadBitmap(bitmap); }
            finally { _ = DeleteObject(bitmap); }
        }
        finally { Marshal.FinalReleaseComObject(factory); }
    }

    private static ShellIconPixels ReadBitmap(IntPtr bitmap)
    {
        if (GetObjectW(bitmap, Marshal.SizeOf<Bitmap>(), out var details) == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        var info = new BitmapInfo
        {
            Header = new BitmapInfoHeader
            {
                Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(), Width = details.Width,
                Height = -Math.Abs(details.Height), Planes = 1, BitCount = 32,
            },
        };
        var pixels = new byte[details.Width * Math.Abs(details.Height) * 4];
        var dc = CreateCompatibleDC(IntPtr.Zero);
        try
        {
            if (GetDIBits(dc, bitmap, 0, (uint)Math.Abs(details.Height), pixels, ref info, 0) == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally { _ = DeleteDC(dc); }
        return new(details.Width, Math.Abs(details.Height), pixels);
    }

    [ComImport, Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(Size size, uint flags, out IntPtr bitmap);
    }

    [StructLayout(LayoutKind.Sequential)] private struct Size { public int Width; public int Height; }
    [StructLayout(LayoutKind.Sequential)] private struct Bitmap
    {
        public int Type, Width, Height, WidthBytes;
        public ushort Planes, BitsPixel;
        public IntPtr Bits;
    }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width, Height;
        public ushort Planes, BitCount;
        public uint Compression, SizeImage;
        public int XPelsPerMeter, YPelsPerMeter;
        public uint ClrUsed, ClrImportant;
    }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo { public BitmapInfoHeader Header; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid id, out IShellItemImageFactory factory);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern int GetObjectW(IntPtr value, int size, out Bitmap bitmap);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern int GetDIBits(IntPtr dc, IntPtr bitmap, uint start, uint lines, byte[] bits, ref BitmapInfo info, uint usage);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(IntPtr value);
}
