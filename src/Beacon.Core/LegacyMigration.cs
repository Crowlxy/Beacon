using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;

namespace Beacon.Core;

public sealed record LegacyMigrationCandidate(string SourceRoot, string SettingsPath);
public sealed record LegacyMigrationResult(bool Success, string Message);

public static class LegacyImporter
{
    public const int MigrationVersion = 1;

    public static LegacyMigrationCandidate? Detect(string roamingAppData, string applicationRoot)
    {
        foreach (var root in new[] { Path.Combine(roamingAppData, "Beacon"), Path.Combine(applicationRoot, "UserData") })
        {
            var settings = Path.Combine(root, "Settings", "Settings.json");
            if (File.Exists(settings)) return new(root, settings);
        }
        return null;
    }

    public static bool WasAttempted(string dataRoot) => File.Exists(Path.Combine(dataRoot, "State", "migration.json"));

    public static LegacyMigrationResult Import(LegacyMigrationCandidate candidate, string dataRoot, DateTimeOffset? now = null)
    {
        var statePath = Path.Combine(dataRoot, "State", "migration.json");
        if (File.Exists(statePath)) return new(false, "移行は既に記録されています。");
        var settingsPath = Path.Combine(dataRoot, "Settings", "r1-settings.json");
        var originalSettings = File.ReadAllBytes(settingsPath);
        var stamp = (now ?? DateTimeOffset.Now).ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var backup = Path.Combine(dataRoot, "Backup", $"legacy-{stamp}");
        try
        {
            CopyDirectory(candidate.SourceRoot, backup);
            var legacy = JsonNode.Parse(File.ReadAllText(candidate.SettingsPath))?.AsObject()
                ?? throw new JsonException("旧設定のルートがオブジェクトではありません。");
            var current = JsonNode.Parse(File.ReadAllText(settingsPath))?.AsObject() ?? [];
            CopyString(legacy, current, "Hotkey", "GlobalHotkey", NormalizeHotkey);
            CopyString(legacy, current, "ColorScheme", "ColorScheme");
            CopyString(legacy, current, "Language", "Language");
            if (legacy["CustomShortcuts"] is JsonArray shortcuts) current["LegacyCustomShortcuts"] = shortcuts.DeepClone();
            AtomicWrite(settingsPath, current.ToJsonString());
            using (JsonDocument.Parse(File.ReadAllText(settingsPath))) { }
            WriteState(statePath, candidate.SourceRoot, "Succeeded", now);
            return new(true, "移行が完了しました。旧データは削除していません。");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            AtomicWrite(settingsPath, Encoding.UTF8.GetString(originalSettings));
            if (Directory.Exists(backup)) Directory.Delete(backup, true);
            WriteState(statePath, candidate.SourceRoot, "Failed", now, exception.Message);
            return new(false, $"移行に失敗しました。変更はロールバックしました: {exception.Message}");
        }
    }

    public static void RecordDeclined(LegacyMigrationCandidate candidate, string dataRoot, DateTimeOffset? now = null)
        => WriteState(Path.Combine(dataRoot, "State", "migration.json"), candidate.SourceRoot, "Declined", now);

    private static void CopyString(JsonObject source, JsonObject target, string sourceName, string targetName, Func<string, string>? transform = null)
    {
        if (source[sourceName]?.GetValue<string>() is not { Length: > 0 } value) return;
        target[targetName] = transform?.Invoke(value) ?? value;
    }

    private static string NormalizeHotkey(string value) => string.Join("+", value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, false);
        }
    }

    private static void AtomicWrite(string path, string content)
    {
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, content, new UTF8Encoding(false));
        File.Move(temporary, path, true);
    }

    private static void WriteState(string path, string source, string result, DateTimeOffset? now, string? error = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicWrite(path, JsonSerializer.Serialize(new { MigrationVersion, Timestamp = now ?? DateTimeOffset.Now, Source = source, Result = result, Error = error }));
    }
}
