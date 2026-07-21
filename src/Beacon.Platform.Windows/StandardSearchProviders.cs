// Windows settings data selectively migrated from Beacon-old/Plugins/Flow.Launcher.Plugin.WindowsSettings/
// and bookmark parsing adapted from Beacon-old/Plugins/Flow.Launcher.Plugin.BrowserBookmark/ (Flow Launcher/Wox, MIT).
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Xml.Linq;
using Beacon.Contracts;

namespace Beacon.Platform.Windows;

public sealed class WindowsSettingsProvider : ISearchProvider
{
    public const string Id = "windows.settings";
    private static readonly Lazy<WindowsSetting[]> Settings = new(Load);
    public string ProviderId => Id;

    public async IAsyncEnumerable<SearchResultDto> SearchAsync(SearchRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (request.Scope is not (QueryScope.All or QueryScope.Settings) || string.IsNullOrWhiteSpace(request.RawQuery)) yield break;
        var query = request.RawQuery.Trim();
        foreach (var setting in Settings.Value)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!setting.Command.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase) ||
                !setting.Terms.Any(x => x.Contains(query, StringComparison.CurrentCultureIgnoreCase))) continue;
            yield return new SearchResultDto
            {
                Id = $"setting:{setting.Command}", ProviderId = Id, Title = setting.Title,
                Subtitle = setting.Area, Kind = ResultKind.Setting,
                Icon = new(IconSource.FluentGlyph, "\uE713"), ExecutionToken = setting.Command,
            };
        }
        await Task.CompletedTask;
    }

    private static WindowsSetting[] Load()
    {
        var assembly = typeof(WindowsSettingsProvider).Assembly;
        using var json = assembly.GetManifestResourceStream("Beacon.Platform.Windows.Data.WindowsSettings.json")!;
        using var resource = assembly.GetManifestResourceStream("Beacon.Platform.Windows.Data.WindowsSettings.ja-JP.xml")!;
        var translations = XDocument.Load(resource).Root!.Elements("data")
            .Where(x => x.Attribute("name") is not null && x.Element("value") is not null)
            .ToDictionary(x => x.Attribute("name")!.Value, x => x.Element("value")!.Value, StringComparer.Ordinal);
        var data = JsonSerializer.Deserialize<WindowsSettingData[]>(json) ?? [];
        return data.Select(x =>
        {
            var title = translations.GetValueOrDefault(x.Name, x.Name);
            var area = translations.GetValueOrDefault(x.Area, x.Area);
            return new WindowsSetting(title, area, x.Command,
                new[] { title, area, x.Name }.Concat(x.AltNames ?? []).ToArray());
        }).ToArray();
    }

    private sealed record WindowsSetting(string Title, string Area, string Command, string[] Terms);
    private sealed record WindowsSettingData(string Name, string Area, string Command, string[]? AltNames);
}

public sealed class BrowserBookmarkProvider : ISearchProvider
{
    public const string Id = "windows.bookmarks";
    private readonly Lazy<Task<Bookmark[]>> _bookmarks = new(() => Task.Run(BookmarkLoader.Load));
    public string ProviderId => Id;

    public async IAsyncEnumerable<SearchResultDto> SearchAsync(SearchRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (request.Scope is not (QueryScope.All or QueryScope.Url) || request.RawQuery.Trim().Length < 2) yield break;
        foreach (var bookmark in await _bookmarks.Value.WaitAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!bookmark.Title.Contains(request.RawQuery, StringComparison.CurrentCultureIgnoreCase) &&
                !bookmark.Url.Contains(request.RawQuery, StringComparison.OrdinalIgnoreCase)) continue;
            yield return new SearchResultDto
            {
                Id = $"bookmark:{bookmark.Url}", ProviderId = Id, Title = bookmark.Title,
                Subtitle = $"{bookmark.Source} · {bookmark.Url}", Kind = ResultKind.Url,
                Icon = new(IconSource.FluentGlyph, "\uE734"), ExecutionToken = bookmark.Url,
            };
        }
    }
}

public sealed class ShellSearchProvider : ISearchProvider
{
    public const string Id = "windows.shell";
    public string ProviderId => Id;
    public async IAsyncEnumerable<SearchResultDto> SearchAsync(SearchRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var command = request.RawQuery.TrimStart();
        if (request.Scope is not (QueryScope.All or QueryScope.Actions) || !command.StartsWith('>')) yield break;
        command = command[1..].Trim();
        if (command.Length == 0) yield break;
        cancellationToken.ThrowIfCancellationRequested();
        yield return new SearchResultDto
        {
            Id = $"shell:{command}", ProviderId = Id, Title = command, Subtitle = "コマンド プロンプトで実行",
            Kind = ResultKind.Action, Icon = new(IconSource.FluentGlyph, "\uE756"), ExecutionToken = command,
        };
        await Task.CompletedTask;
    }
}

public sealed class ProcessKillerSearchProvider : ISearchProvider
{
    public const string Id = "windows.process-killer";
    public string ProviderId => Id;
    public async IAsyncEnumerable<SearchResultDto> SearchAsync(SearchRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (request.Scope is not (QueryScope.All or QueryScope.Actions) ||
            !request.RawQuery.StartsWith("kill ", StringComparison.OrdinalIgnoreCase)) yield break;
        var query = request.RawQuery[5..].Trim();
        foreach (var process in Process.GetProcesses().OrderBy(x => x.ProcessName))
        {
            using (process)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!process.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
                var subtitle = $"PID {process.Id}";
                try { _ = process.Handle; }
                catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
                {
                    subtitle += " · 終了には管理者権限が必要です";
                }
                yield return new SearchResultDto
                {
                    Id = $"process:{process.Id}", ProviderId = Id, Title = process.ProcessName, Subtitle = subtitle,
                    Kind = ResultKind.Action, Icon = new(IconSource.FluentGlyph, "\uE8BB"), ExecutionToken = process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                };
            }
        }
        await Task.CompletedTask;
    }
}

public sealed class SystemActionSearchProvider : ISearchProvider
{
    public const string Id = "windows.system-actions";
    private static readonly (string Token, string Title, string[] Terms)[] Actions =
    [
        ("shutdown", "シャットダウン", ["shutdown", "電源オフ"]),
        ("restart", "再起動", ["restart", "再起動"]),
        ("sleep", "スリープ", ["sleep", "スリープ"]),
        ("lock", "ロック", ["lock", "ロック"]),
        ("empty-recycle-bin", "ごみ箱を空にする", ["recycle bin", "ごみ箱"]),
    ];
    public string ProviderId => Id;

    public static IReadOnlyList<SearchResultDto> Browse(string query = "") => Actions
        .Where(action => string.IsNullOrWhiteSpace(query) || action.Terms.Any(x => x.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
        .Select(CreateResult)
        .ToArray();

    public async IAsyncEnumerable<SearchResultDto> SearchAsync(SearchRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (request.Scope is not (QueryScope.All or QueryScope.Actions) || string.IsNullOrWhiteSpace(request.RawQuery)) yield break;
        foreach (var result in Browse(request.RawQuery))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return result;
        }
        await Task.CompletedTask;
    }

    private static SearchResultDto CreateResult((string Token, string Title, string[] Terms) action) => new()
    {
        Id = $"system:{action.Token}", ProviderId = Id, Title = action.Title, Subtitle = "システム操作",
        Kind = ResultKind.Action, Icon = new(IconSource.FluentGlyph, "\uE7E8"), ExecutionToken = action.Token,
    };
}

internal sealed record Bookmark(string Title, string Url, string Source);

internal static class BookmarkLoader
{
    public static Bookmark[] Load() => LoadChromium().Concat(LoadFirefox()).DistinctBy(x => x.Url).ToArray();

    internal static Bookmark[] LoadChromiumFile(string path, string source)
    {
        if (!File.Exists(path)) return [];
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("roots", out var roots)) return [];
        var results = new List<Bookmark>();
        WalkChromium(roots, source, results);
        return results.ToArray();
    }

    private static IEnumerable<Bookmark> LoadChromium()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roots = new[]
        {
            (Path.Combine(local, "Google", "Chrome", "User Data"), "Chrome"),
            (Path.Combine(local, "Microsoft", "Edge", "User Data"), "Edge"),
            (Path.Combine(local, "BraveSoftware", "Brave-Browser", "User Data"), "Brave"),
        };
        foreach (var (root, source) in roots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var profile in Directory.EnumerateDirectories(root))
                foreach (var bookmark in LoadChromiumFile(Path.Combine(profile, "Bookmarks"), source)) yield return bookmark;
        }
    }

    private static void WalkChromium(JsonElement element, string source, List<Bookmark> results)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("url", out var url) && element.TryGetProperty("name", out var name) &&
                Uri.TryCreate(url.GetString(), UriKind.Absolute, out _))
                results.Add(new(name.GetString() ?? url.GetString()!, url.GetString()!, source));
            foreach (var property in element.EnumerateObject()) WalkChromium(property.Value, source, results);
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) WalkChromium(child, source, results);
    }

    private static IEnumerable<Bookmark> LoadFirefox()
    {
        var firefox = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mozilla", "Firefox", "Profiles");
        if (!Directory.Exists(firefox)) yield break;
        foreach (var database in Directory.EnumerateFiles(firefox, "places.sqlite", SearchOption.AllDirectories))
            foreach (var bookmark in WindowsSqlite.ReadFirefoxBookmarks(database)) yield return bookmark;
    }
}

internal static partial class WindowsSqlite
{
    private const int OpenReadOnly = 1;
    private const int Row = 100;

    public static Bookmark[] ReadFirefoxBookmarks(string path)
    {
        var temporary = Path.Combine(Path.GetTempPath(), $"Beacon-{Guid.NewGuid():N}.sqlite");
        try
        {
            File.Copy(path, temporary, true);
            if (sqlite3_open_v2(temporary, out var database, OpenReadOnly, null) != 0) return [];
            try
            {
                const string sql = "SELECT moz_bookmarks.title, moz_places.url FROM moz_bookmarks JOIN moz_places ON moz_bookmarks.fk=moz_places.id WHERE moz_bookmarks.title IS NOT NULL LIMIT 2000";
                if (sqlite3_prepare_v2(database, sql, -1, out var statement, IntPtr.Zero) != 0) return [];
                try
                {
                    var results = new List<Bookmark>();
                    while (sqlite3_step(statement) == Row)
                    {
                        var title = Marshal.PtrToStringUTF8(sqlite3_column_text(statement, 0));
                        var url = Marshal.PtrToStringUTF8(sqlite3_column_text(statement, 1));
                        if (!string.IsNullOrWhiteSpace(title) && Uri.TryCreate(url, UriKind.Absolute, out _)) results.Add(new(title, url!, "Firefox"));
                    }
                    return results.ToArray();
                }
                finally { _ = sqlite3_finalize(statement); }
            }
            finally { _ = sqlite3_close(database); }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DllNotFoundException)
        {
            return [];
        }
        finally
        {
            try { File.Delete(temporary); } catch (IOException) { }
        }
    }

    [LibraryImport("winsqlite3.dll", StringMarshalling = StringMarshalling.Utf8)] private static partial int sqlite3_open_v2(string fileName, out IntPtr database, int flags, string? vfs);
    [LibraryImport("winsqlite3.dll", StringMarshalling = StringMarshalling.Utf8)] private static partial int sqlite3_prepare_v2(IntPtr database, string sql, int bytes, out IntPtr statement, IntPtr tail);
    [DllImport("winsqlite3.dll")] private static extern int sqlite3_step(IntPtr statement);
    [DllImport("winsqlite3.dll")] private static extern IntPtr sqlite3_column_text(IntPtr statement, int column);
    [DllImport("winsqlite3.dll")] private static extern int sqlite3_finalize(IntPtr statement);
    [DllImport("winsqlite3.dll")] private static extern int sqlite3_close(IntPtr database);
}
