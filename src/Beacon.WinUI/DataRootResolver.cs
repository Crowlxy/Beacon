using System.Text;
using System.Text.Json;

namespace Beacon.WinUI;

public sealed record DataRootResolution(string Path, bool IsPortable);

public sealed class DataRootResolutionException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public static class DataRootResolver
{
    private static readonly string[] Subdirectories =
        ["Settings", "History", "Plugins", "Cache", "Logs", "Clipboard", "State"];

    public static DataRootResolution Resolve(string executableRoot, string localAppDataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executableRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(localAppDataRoot);

        var root = Path.GetFullPath(executableRoot);
        var portableFlag = Path.Combine(root, "portable.flag");
        var portableData = Path.Combine(root, "Data");
        var hasFlag = File.Exists(portableFlag);
        var hasPortableData = Directory.Exists(portableData);

        if (!hasFlag && hasPortableData)
        {
            throw new DataRootResolutionException(
                "portable.flag is missing while Data exists. Restore portable.flag to continue portable use, or explicitly migrate Data before using non-portable mode.");
        }

        var dataRoot = hasFlag
            ? portableData
            : Path.Combine(Path.GetFullPath(localAppDataRoot), "Beacon", "Data");

        try
        {
            Directory.CreateDirectory(dataRoot);
            foreach (var name in Subdirectories)
            {
                Directory.CreateDirectory(Path.Combine(dataRoot, name));
            }

            var probe = Path.Combine(dataRoot, $".write-probe-{Guid.NewGuid():N}");
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
            {
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            throw new DataRootResolutionException(
                $"Beacon cannot write to '{dataRoot}'. Move the Beacon folder to a writable location. Data was not redirected to AppData.",
                exception);
        }

        return new DataRootResolution(Path.GetFullPath(dataRoot), hasFlag);
    }
}

internal static class R1Storage
{
    private static string? _dataRoot;

    public static void Initialize(string dataRoot)
    {
        _dataRoot = dataRoot;
        var settingsPath = Path.Combine(dataRoot, "Settings", "r1-settings.json");
        if (!File.Exists(settingsPath))
        {
            var temporaryPath = settingsPath + ".tmp";
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(new { ContractVersion = Contracts.ContractVersion.Current }),
                new UTF8Encoding(false));
            File.Move(temporaryPath, settingsPath, true);
        }

        WriteLog("INFO Beacon.Next started");
    }

    public static void WriteLog(string message)
    {
        if (_dataRoot is null)
        {
            return;
        }

        var path = Path.Combine(_dataRoot, "Logs", "beacon.log");
        using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.WriteLine($"{DateTimeOffset.Now:O} {message}");
    }
}
