using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Beacon.WinUI;

internal sealed class NativeWindowController : IDisposable
{
    private const int WindowProcedureIndex = -4;
    private const uint HotkeyMessage = 0x0312;
    private const uint DwmCompositionChangedMessage = 0x031E;
    private const uint TrayMessage = 0x8001;
    private const uint LeftButtonUp = 0x0202;
    private const uint RightButtonUp = 0x0205;
    private const uint HotkeyId = 0xB001;
    private const uint ShowMenuId = 1;
    private const uint ExitMenuId = 2;
    private const uint NotifyIconAdd = 0;
    private const uint NotifyIconDelete = 2;
    private const uint NotifyIconSetVersion = 4;
    private const uint NotifyIconMessage = 1;
    private const uint NotifyIconIcon = 2;
    private const uint NotifyIconTip = 4;
    private const uint NotifyIconVersion4 = 4;
    private const uint MenuString = 0;
    private const uint TrackReturnCommand = 0x0100;
    private const uint ModifierAlt = 0x0001;
    private const uint ModifierShift = 0x0004;
    private const uint ModifierNoRepeat = 0x4000;
    private const uint VirtualKeySpace = 0x20;
    private const int DefaultApplicationIcon = 32512;

    private readonly IntPtr _windowHandle;
    private readonly Action _show;
    private readonly Action _exit;
    private readonly NativeMethods.WindowProcedure _windowProcedure;
    private readonly IntPtr _previousWindowProcedure;
    private NativeMethods.NotifyIconData _notifyIconData;
    private bool _disposed;

    public NativeWindowController(IntPtr windowHandle, Action show, Action exit)
    {
        _windowHandle = windowHandle;
        _show = show;
        _exit = exit;
        _windowProcedure = WindowProcedure;
        _previousWindowProcedure = NativeMethods.SetWindowLongPtrW(
            windowHandle,
            WindowProcedureIndex,
            Marshal.GetFunctionPointerForDelegate(_windowProcedure));
        if (_previousWindowProcedure == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to attach the Beacon window procedure.");
        }

        if (!NativeMethods.RegisterHotKey(
                windowHandle,
                HotkeyId,
                ModifierAlt | ModifierShift | ModifierNoRepeat,
                VirtualKeySpace))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Alt+Shift+Space is already registered.");
        }

        _notifyIconData = new NativeMethods.NotifyIconData
        {
            Size = Marshal.SizeOf<NativeMethods.NotifyIconData>(),
            WindowHandle = windowHandle,
            Id = 1,
            Flags = NotifyIconMessage | NotifyIconIcon | NotifyIconTip,
            CallbackMessage = TrayMessage,
            IconHandle = NativeMethods.LoadIconW(IntPtr.Zero, (IntPtr)DefaultApplicationIcon),
            Tip = "Beacon.Next",
            Info = string.Empty,
            InfoTitle = string.Empty,
        };
        if (!NativeMethods.ShellNotifyIconW(NotifyIconAdd, ref _notifyIconData))
        {
            Dispose();
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to add the Beacon tray icon.");
        }

        _notifyIconData.Version = NotifyIconVersion4;
        NativeMethods.ShellNotifyIconW(NotifyIconSetVersion, ref _notifyIconData);
    }

    private IntPtr WindowProcedure(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == DwmCompositionChangedMessage && !NativeMethods.ApplyWindowFrameStyle(windowHandle))
        {
            R1Storage.WriteLog("ERROR DWM rounded corners or border suppression failed after composition changed");
        }

        if (message == HotkeyMessage && unchecked((uint)wParam.ToInt64()) == HotkeyId)
        {
            _show();
            return IntPtr.Zero;
        }

        if (message == TrayMessage)
        {
            var mouseMessage = unchecked((uint)lParam.ToInt64()) & 0xFFFF;
            if (mouseMessage == LeftButtonUp)
            {
                _show();
            }
            else if (mouseMessage == RightButtonUp)
            {
                ShowContextMenu();
            }

            return IntPtr.Zero;
        }

        return NativeMethods.CallWindowProcW(_previousWindowProcedure, windowHandle, message, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        var menu = NativeMethods.CreatePopupMenu();
        if (menu == IntPtr.Zero)
        {
            return;
        }

        try
        {
            NativeMethods.AppendMenuW(menu, MenuString, ShowMenuId, "Show Beacon");
            NativeMethods.AppendMenuW(menu, MenuString, ExitMenuId, "Exit Beacon");
            NativeMethods.GetCursorPos(out var point);
            NativeMethods.SetForegroundWindow(_windowHandle);
            var command = NativeMethods.TrackPopupMenuEx(
                menu,
                TrackReturnCommand,
                point.X,
                point.Y,
                _windowHandle,
                IntPtr.Zero);
            if (command == ShowMenuId)
            {
                _show();
            }
            else if (command == ExitMenuId)
            {
                _exit();
            }
        }
        finally
        {
            NativeMethods.DestroyMenu(menu);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        NativeMethods.UnregisterHotKey(_windowHandle, HotkeyId);
        if (_notifyIconData.Size != 0)
        {
            NativeMethods.ShellNotifyIconW(NotifyIconDelete, ref _notifyIconData);
        }

        if (_previousWindowProcedure != IntPtr.Zero)
        {
            NativeMethods.SetWindowLongPtrW(_windowHandle, WindowProcedureIndex, _previousWindowProcedure);
        }
    }
}

internal static class NativeMethods
{
    private const int DwmWindowCornerPreference = 33;
    private const int DwmCornerRound = 2;
    private const int DwmBorderColor = 34;
    private const int DwmColorNone = unchecked((int)0xFFFFFFFE);
    private const int DwmNonClientRenderingPolicy = 2;
    private const int DwmNonClientRenderingDisabled = 1;
    private const int WindowStyleIndex = -16;
    private const long WindowDialogFrameStyle = 0x00400000L;
    private const uint FrameChangedFlags = 0x0037;
    public const uint MessageBoxIconError = 0x00000010;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate IntPtr WindowProcedure(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        public int X;
        public int Y;
    }

    internal static bool ApplyWindowFrameStyle(IntPtr windowHandle)
    {
        var style = GetWindowLongPtrW(windowHandle, WindowStyleIndex);
        var styleWithoutFrame = new IntPtr(style.ToInt64() & ~WindowDialogFrameStyle);
        var frameRemoved = styleWithoutFrame == style ||
            (SetWindowLongPtrW(windowHandle, WindowStyleIndex, styleWithoutFrame) != IntPtr.Zero &&
             SetWindowPos(windowHandle, IntPtr.Zero, 0, 0, 0, 0, FrameChangedFlags));
        var nonClientPolicy = DwmNonClientRenderingDisabled;
        var preference = DwmCornerRound;
        var borderColor = DwmColorNone;
        var nonClientDisabled = DwmSetWindowAttribute(
            windowHandle,
            DwmNonClientRenderingPolicy,
            ref nonClientPolicy,
            sizeof(int)) >= 0;
        var cornersApplied = DwmSetWindowAttribute(
            windowHandle,
            DwmWindowCornerPreference,
            ref preference,
            sizeof(int)) >= 0;
        var borderSuppressed = DwmSetWindowAttribute(
            windowHandle,
            DwmBorderColor,
            ref borderColor,
            sizeof(int)) >= 0;
        return frameRemoved && nonClientDisabled && cornersApplied && borderSuppressed;
    }

    internal static bool ApplyRoundedRegion(IntPtr windowHandle, int width, int height, int radius)
    {
        var region = CreateRoundRectRgn(0, 0, width, height, radius * 2, radius * 2);
        if (region == IntPtr.Zero) return false;
        if (SetWindowRgn(windowHandle, region, true) != 0) return true;
        _ = DeleteObject(region);
        return false;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct NotifyIconData
    {
        public int Size;
        public IntPtr WindowHandle;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public IntPtr IconHandle;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Tip;

        public uint State;
        public uint StateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Info;

        public uint Version;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string InfoTitle;

        public uint InfoFlags;
        public Guid ItemGuid;
        public IntPtr BalloonIconHandle;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(IntPtr windowHandle, uint id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(IntPtr windowHandle, uint id);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr windowHandle, int attribute, ref int value, int valueSize);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static extern IntPtr SetWindowLongPtrW(IntPtr windowHandle, int index, IntPtr newValue);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtrW(IntPtr windowHandle, int index);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(
        int left,
        int top,
        int right,
        int bottom,
        int ellipseWidth,
        int ellipseHeight);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr windowHandle, IntPtr region, [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr value);

    [DllImport("user32.dll")]
    internal static extern IntPtr CallWindowProcW(
        IntPtr previousWindowProcedure,
        IntPtr windowHandle,
        uint message,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShellNotifyIconW(uint message, ref NotifyIconData data);

    [DllImport("user32.dll")]
    internal static extern IntPtr LoadIconW(IntPtr instance, IntPtr iconName);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AppendMenuW(IntPtr menu, uint flags, uint item, string text);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    internal static extern uint GetDpiForWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    internal static extern IntPtr MonitorFromPoint(Point point, uint flags);

    [DllImport("shcore.dll")]
    internal static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint TrackPopupMenuEx(
        IntPtr menu,
        uint flags,
        int x,
        int y,
        IntPtr windowHandle,
        IntPtr parameters);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int MessageBoxW(IntPtr windowHandle, string text, string caption, uint type);
}
