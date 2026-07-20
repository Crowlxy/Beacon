using System.Text;
using System.Text.Json;

namespace Beacon.WinUI;

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
