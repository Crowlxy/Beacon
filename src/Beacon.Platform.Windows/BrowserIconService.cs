using System.Collections.Concurrent;
using Beacon.Contracts;
using Microsoft.Win32;

namespace Beacon.Platform.Windows;

/// <summary>
/// 検索結果に「実際に開くブラウザ」のアイコンを割り当てる。
/// 実行ファイルの所在は Windows の App Paths（HKCU/HKLM）で解決し、既定ブラウザは
/// https の UserChoice ProgId から open コマンドを辿って解決する。いずれも公開レジストリ契約。
/// </summary>
public static class BrowserIconService
{
    private const string AppPathsKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
    private const string HttpsUserChoiceKey = @"SOFTWARE\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice";

    /// <summary>ブックマークの取得元名（<see cref="BookmarkLoader"/> が付ける表記）と実行ファイル名の対応。</summary>
    private static readonly Dictionary<string, string> ExecutableNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Chrome"] = "chrome.exe",
        ["Edge"] = "msedge.exe",
        ["Brave"] = "brave.exe",
        ["Firefox"] = "firefox.exe",
        ["Vivaldi"] = "vivaldi.exe",
        ["Opera"] = "opera.exe",
    };

    private static readonly ConcurrentDictionary<string, string?> ExecutablePaths = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lazy<string?> DefaultBrowserPath = new(ResolveDefaultBrowser, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>指定ブラウザの実行ファイルアイコン。実行ファイルを特定できない場合は null。</summary>
    public static IconDescriptor? ForBrowser(string browser)
    {
        if (!ExecutableNames.TryGetValue(browser, out var executable)) return null;
        var path = ExecutablePaths.GetOrAdd(executable, ResolveFromAppPaths);
        return path is null ? null : new IconDescriptor(IconSource.FileShellIcon, path);
    }

    /// <summary>既定ブラウザの実行ファイルアイコン。特定できない場合は null。</summary>
    public static IconDescriptor? ForDefaultBrowser() =>
        DefaultBrowserPath.Value is { } path ? new IconDescriptor(IconSource.FileShellIcon, path) : null;

    private static string? ResolveFromAppPaths(string executable)
    {
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            var path = ReadDefaultValue(hive, $@"{AppPathsKey}\{executable}");
            if (ExistingFile(path) is { } resolved) return resolved;
        }
        return null;
    }

    private static string? ResolveDefaultBrowser()
    {
        var progId = ReadValue(Registry.CurrentUser, HttpsUserChoiceKey, "ProgId");
        if (string.IsNullOrWhiteSpace(progId)) return null;
        var command = ReadDefaultValue(Registry.ClassesRoot, $@"{progId}\shell\open\command");
        return ExistingFile(ExecutableFromCommand(command));
    }

    /// <summary>「"C:\...\chrome.exe" --single-argument %1」形式のコマンド行から実行ファイルパスだけを取り出す。</summary>
    internal static string? ExecutableFromCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        var value = command.Trim();
        if (value.StartsWith('"'))
        {
            var closing = value.IndexOf('"', 1);
            return closing > 1 ? value[1..closing] : null;
        }
        var extension = value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return extension < 0 ? null : value[..(extension + 4)];
    }

    private static string? ReadDefaultValue(RegistryKey hive, string path) => ReadValue(hive, path, null);

    private static string? ReadValue(RegistryKey hive, string path, string? name)
    {
        try
        {
            using var key = hive.OpenSubKey(path);
            return key?.GetValue(name) as string;
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private static string? ExistingFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var trimmed = path.Trim().Trim('"');
        return File.Exists(trimmed) ? trimmed : null;
    }
}
