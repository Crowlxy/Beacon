using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace Beacon.Platform.Windows;

public sealed record ActionExecutionResult(bool Success, string? FailureReason = null);

public static partial class BuiltInActionService
{
    private const uint OpenAsExecute = 0x00000004;

    public static ActionExecutionResult Execute(
        string actionId,
        string sourcePath,
        string? argument = null,
        nint ownerWindow = default,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryGetWebUri(sourcePath, out var uri))
            {
                if (actionId == "open") ProcessLaunchService.Start(uri.AbsoluteUri);
                else if (actionId == "copy-path") ClipboardTextService.Set(uri.AbsoluteUri);
                else return new(false, "URLにはこのアクションを使用できません。");
                return new(true);
            }

            var source = Path.GetFullPath(sourcePath);
            if (!File.Exists(source) && !Directory.Exists(source)) return new(false, "対象が見つかりません。");
            switch (actionId)
            {
                case "open": ProcessLaunchService.Start(source); break;
                case "open-with": OpenWith(ownerWindow, source); break;
                case "reveal": FileOperationService.ShowInFolder(source); break;
                case "copy-path": ClipboardTextService.Set(source); break;
                case "run-admin": Process.Start(new ProcessStartInfo(source) { UseShellExecute = true, Verb = "runas" }); break;
                case "rename": Rename(source, Required(argument)); break;
                case "copy": Copy(source, RequiredDirectory(argument), cancellationToken); break;
                case "move": Move(source, RequiredDirectory(argument)); break;
                case "zip": Zip(source, cancellationToken); break;
                case "terminal": ShellExecutionService.Run(string.Empty, Directory.Exists(source) ? source : Path.GetDirectoryName(source)); break;
                default: return new(false, "未知のアクションです。");
            }
            return new(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception or COMException)
        {
            return new(false, exception.Message);
        }
    }

    private static void OpenWith(nint ownerWindow, string source)
    {
        if (!File.Exists(source)) throw new ArgumentException("プログラムから開く対象はファイルに限られます。");
        var filePointer = Marshal.StringToCoTaskMemUni(source);
        try
        {
            var information = new OpenAsInfo(filePointer, 0, OpenAsExecute);
            Marshal.ThrowExceptionForHR(SHOpenWithDialog(ownerWindow, in information));
        }
        finally
        {
            Marshal.FreeCoTaskMem(filePointer);
        }
    }

    private static void Rename(string source, string name)
    {
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new ArgumentException("名前に使用できない文字があります。");
        var destination = Path.Combine(Path.GetDirectoryName(source)!, name);
        if (Directory.Exists(source)) Directory.Move(source, destination); else File.Move(source, destination);
    }

    private static void Copy(string source, string destinationDirectory, CancellationToken cancellationToken)
    {
        var destination = Path.Combine(destinationDirectory, Path.GetFileName(source));
        cancellationToken.ThrowIfCancellationRequested();
        if (Directory.Exists(source)) CopyDirectory(source, destination, cancellationToken); else File.Copy(source, destination, overwrite: false);
    }

    private static void Move(string source, string destinationDirectory)
    {
        var destination = Path.Combine(destinationDirectory, Path.GetFileName(source));
        if (Directory.Exists(source)) Directory.Move(source, destination); else File.Move(source, destination);
    }

    private static void Zip(string source, CancellationToken cancellationToken)
    {
        var destination = source.TrimEnd(Path.DirectorySeparatorChar) + ".zip";
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var archive = ZipFile.Open(destination, ZipArchiveMode.Create);
            if (Directory.Exists(source))
            {
                foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    archive.CreateEntryFromFile(file, Path.GetRelativePath(source, file));
                }
            }
            else archive.CreateEntryFromFile(source, Path.GetFileName(source));
        }
        catch (OperationCanceledException) { File.Delete(destination); throw; }
    }

    private static void CopyDirectory(string source, string destination, CancellationToken cancellationToken)
    {
        if (Directory.Exists(destination)) throw new IOException("コピー先に同名フォルダーがあります。");
        Directory.CreateDirectory(destination);
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
            }
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)));
            }
        }
        catch (OperationCanceledException) { Directory.Delete(destination, true); throw; }
    }

    private static bool TryGetWebUri(string value, out Uri uri) =>
        Uri.TryCreate(value, UriKind.Absolute, out uri!) && uri.Scheme is "http" or "https";

    private static string Required(string? value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("入力が必要です。") : value;

    private static string RequiredDirectory(string? value)
    {
        var path = Path.GetFullPath(Required(value));
        return Directory.Exists(path) ? path : throw new ArgumentException("移動先フォルダーが見つかりません。");
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct OpenAsInfo(nint File, nint Class, uint Flags);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SHOpenWithDialog(nint ownerWindow, in OpenAsInfo openAsInfo);
}
