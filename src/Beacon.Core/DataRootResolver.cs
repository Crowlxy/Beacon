namespace Beacon.Core;

public sealed record DataRootResolution(string Path, bool IsPortable);
public sealed class DataRootResolutionException(string message, Exception? innerException = null) : Exception(message, innerException);

public static class DataRootResolver
{
    private static readonly string[] Subdirectories = ["Settings", "History", "Plugins", "Cache", "Logs", "Clipboard", "State"];

    public static DataRootResolution Resolve(string executableRoot, string localAppDataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executableRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(localAppDataRoot);
        var root = Path.GetFullPath(executableRoot);
        var portableFlag = Path.Combine(root, "portable.flag");
        var portableData = Path.Combine(root, "Data");
        var hasFlag = File.Exists(portableFlag);
        if (!hasFlag && Directory.Exists(portableData))
            throw new DataRootResolutionException("portable.flag is missing while Data exists. Restore portable.flag to continue portable use, or explicitly migrate Data before using non-portable mode.");

        var dataRoot = hasFlag ? portableData : Path.Combine(Path.GetFullPath(localAppDataRoot), "Beacon", "Data");
        try
        {
            Directory.CreateDirectory(dataRoot);
            foreach (var name in Subdirectories) Directory.CreateDirectory(Path.Combine(dataRoot, name));
            var probe = Path.Combine(dataRoot, $".write-probe-{Guid.NewGuid():N}");
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            throw new DataRootResolutionException($"Beacon cannot write to '{dataRoot}'. Move the Beacon folder to a writable location. Data was not redirected to AppData.", exception);
        }
        return new DataRootResolution(Path.GetFullPath(dataRoot), hasFlag);
    }
}
