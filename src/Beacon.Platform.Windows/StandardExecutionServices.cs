using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Beacon.Platform.Windows;

public static class ShellExecutionService
{
    public static Process? Run(string command, string? workingDirectory) => Process.Start(new ProcessStartInfo("cmd.exe", "/k " + command)
    {
        UseShellExecute = true,
        WorkingDirectory = Directory.Exists(workingDirectory) ? workingDirectory : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    });
}

public static class ProcessTerminationService
{
    public static void Terminate(string processId)
    {
        if (!int.TryParse(processId, out var id) || id <= 0) throw new ArgumentException("Invalid process id.", nameof(processId));
        using var process = Process.GetProcessById(id);
        process.Kill(entireProcessTree: true);
    }
}

public static class SystemActionService
{
    public static bool RequiresConfirmation(string action) => action is "shutdown" or "restart" or "empty-recycle-bin";

    public static void Execute(string action)
    {
        switch (action)
        {
            case "shutdown": ProcessLaunchService.Start("shutdown.exe", "/s /t 0"); break;
            case "restart": ProcessLaunchService.Start("shutdown.exe", "/r /t 0"); break;
            case "sleep":
                if (!SetSuspendState(false, false, false)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                break;
            case "lock":
                if (!LockWorkStation()) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                break;
            case "empty-recycle-bin":
                var result = SHEmptyRecycleBin(IntPtr.Zero, null, 0x0001 | 0x0002 | 0x0004);
                if (result != 0) Marshal.ThrowExceptionForHR(result);
                break;
            default: throw new ArgumentException("Unknown system action.", nameof(action));
        }
    }

    [DllImport("powrprof.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LockWorkStation();

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr windowHandle, string? rootPath, uint flags);
}
