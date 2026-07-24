using Microsoft.Win32;

namespace Beacon.Platform.Windows;

public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Beacon.Next";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool EnsureCurrentPath(string executablePath, Action<string>? log = null)
    {
        try
        {
            var expected = CommandFor(executablePath);
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key?.GetValue(ValueName) is not string current) return false;
            if (string.Equals(current, expected, StringComparison.OrdinalIgnoreCase)) return true;
            key.SetValue(ValueName, expected, RegistryValueKind.String);
            log?.Invoke("INFO Startup registration updated after portable path change");
            return true;
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException)
        {
            log?.Invoke($"WARN Startup registration unavailable: {exception.Message}");
            return false;
        }
    }

    public static void SetEnabled(bool enabled, string executablePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (enabled) key.SetValue(ValueName, CommandFor(executablePath), RegistryValueKind.String);
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    internal static string CommandFor(string executablePath) => $"\"{Path.GetFullPath(executablePath)}\"";
}
