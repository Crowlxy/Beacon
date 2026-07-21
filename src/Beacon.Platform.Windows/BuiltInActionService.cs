using System.Diagnostics;
using System.IO.Compression;

namespace Beacon.Platform.Windows;

public sealed record ActionExecutionResult(bool Success, string? FailureReason = null);

public static class BuiltInActionService
{
    public static ActionExecutionResult Execute(string actionId, string sourcePath, string? argument = null)
    {
        try
        {
            var source = Path.GetFullPath(sourcePath);
            if (!File.Exists(source) && !Directory.Exists(source)) return new(false, "対象が見つかりません。");
            switch (actionId)
            {
                case "open":
                case "open-with": ProcessLaunchService.Start(source); break;
                case "reveal": FileOperationService.ShowInFolder(source); break;
                case "copy-path": ClipboardTextService.Set(source); break;
                case "run-admin": Process.Start(new ProcessStartInfo(source) { UseShellExecute = true, Verb = "runas" }); break;
                case "rename": Rename(source, Required(argument)); break;
                case "copy": Copy(source, RequiredDirectory(argument)); break;
                case "move": Move(source, RequiredDirectory(argument)); break;
                case "zip": Zip(source); break;
                case "terminal": ShellExecutionService.Run(string.Empty, Directory.Exists(source) ? source : Path.GetDirectoryName(source)); break;
                default: return new(false, "未知のアクションです。");
            }
            return new(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception)
        {
            return new(false, exception.Message);
        }
    }

    private static void Rename(string source, string name)
    {
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new ArgumentException("名前に使用できない文字があります。");
        var destination = Path.Combine(Path.GetDirectoryName(source)!, name);
        if (Directory.Exists(source)) Directory.Move(source, destination); else File.Move(source, destination);
    }

    private static void Copy(string source, string destinationDirectory)
    {
        var destination = Path.Combine(destinationDirectory, Path.GetFileName(source));
        if (Directory.Exists(source)) CopyDirectory(source, destination); else File.Copy(source, destination, overwrite: false);
    }

    private static void Move(string source, string destinationDirectory)
    {
        var destination = Path.Combine(destinationDirectory, Path.GetFileName(source));
        if (Directory.Exists(source)) Directory.Move(source, destination); else File.Move(source, destination);
    }

    private static void Zip(string source)
    {
        var destination = source.TrimEnd(Path.DirectorySeparatorChar) + ".zip";
        if (Directory.Exists(source)) ZipFile.CreateFromDirectory(source, destination);
        else
        {
            using var archive = ZipFile.Open(destination, ZipArchiveMode.Create);
            archive.CreateEntryFromFile(source, Path.GetFileName(source));
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)));
    }

    private static string Required(string? value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("入力が必要です。") : value;
    private static string RequiredDirectory(string? value)
    {
        var path = Path.GetFullPath(Required(value));
        return Directory.Exists(path) ? path : throw new ArgumentException("移動先フォルダーが見つかりません。");
    }
}
