using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Beacon.WinUI;

internal static class R1Storage
{
    private const int DefaultLogRetentionCount = 14;
    private static readonly object LogLock = new();
    private static string? _dataRoot;
    private static string? _runMarkerPath;

    public static void Initialize(string dataRoot)
    {
        _dataRoot = dataRoot;
        var settingsPath = Path.Combine(dataRoot, "Settings", "r1-settings.json");
        if (!File.Exists(settingsPath))
        {
            var temporaryPath = settingsPath + ".tmp";
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(new
                {
                    ContractVersion = Contracts.ContractVersion.Current,
                    LogRetentionCount = DefaultLogRetentionCount,
                    ClipboardEnabled = false,
                    PersonalizationEnabled = true,
                    QuickKeys = new { rf = "reveal", cp = "copy-path", rn = "rename", term = "terminal" },
                }),
                new UTF8Encoding(false));
            File.Move(temporaryPath, settingsPath, true);
        }

        RotateLogs(dataRoot, ReadRetentionCount(settingsPath));
        _runMarkerPath = Path.Combine(dataRoot, "State", "running.marker");
        var previousRunFailed = File.Exists(_runMarkerPath);
        File.WriteAllText(_runMarkerPath, DateTimeOffset.Now.ToString("O"), new UTF8Encoding(false));
        WriteLog("INFO Beacon.Next started");
        if (previousRunFailed) WriteLog("WARN Previous Beacon.Next run ended unexpectedly");
    }

    public static void WriteLog(string message)
    {
        if (_dataRoot is null)
        {
            return;
        }

        lock (LogLock)
        {
            var path = Path.Combine(_dataRoot, "Logs", $"beacon-{DateTime.Now:yyyy-MM-dd}.log");
            using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.WriteLine($"{DateTimeOffset.Now:O} {message}");
        }
    }

    public static void MarkCleanExit()
    {
        if (_runMarkerPath is not null) File.Delete(_runMarkerPath);
    }

    public static bool GetBoolean(string name, bool defaultValue)
    {
        if (_dataRoot is null) return defaultValue;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(_dataRoot, "Settings", "r1-settings.json")));
            return document.RootElement.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? value.GetBoolean()
                : defaultValue;
        }
        catch (Exception exception) when (exception is IOException or JsonException) { WriteLog($"WARN Settings read failed: {exception.Message}"); return defaultValue; }
    }

    public static void SetBoolean(string name, bool value)
    {
        if (_dataRoot is null) return;
        var path = Path.Combine(_dataRoot, "Settings", "r1-settings.json");
        try
        {
            var root = JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? [];
            root[name] = value;
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, root.ToJsonString(), new UTF8Encoding(false));
            File.Move(temporary, path, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            WriteLog($"WARN Settings write failed: {exception.Message}");
        }
    }

    internal static void RotateLogs(string dataRoot, int retentionCount)
    {
        var logDirectory = Path.Combine(dataRoot, "Logs");
        Directory.CreateDirectory(logDirectory);
        foreach (var path in Directory.EnumerateFiles(logDirectory, "beacon-????-??-??.log")
                     .OrderByDescending(Path.GetFileName)
                     .Skip(Math.Max(1, retentionCount)))
            File.Delete(path);
    }

    private static int ReadRetentionCount(string settingsPath)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            return document.RootElement.TryGetProperty("LogRetentionCount", out var value) && value.TryGetInt32(out var count) && count > 0
                ? count
                : DefaultLogRetentionCount;
        }
        catch (JsonException)
        {
            return DefaultLogRetentionCount;
        }
    }
}
