// Selectively adapted from Beacon-old/Plugins/Flow.Launcher.Plugin.Explorer/Search/WindowsIndex/
// WindowsIndex.cs and QueryConstructor.cs (Flow Launcher/Wox, MIT).
using System.Data.OleDb;
using System.Text.RegularExpressions;
using Beacon.Contracts;

namespace Beacon.Platform.Windows;

public sealed partial class WindowsIndexSearch(Action<string>? log = null)
{
    public const string ConnectionString = "Provider=Search.CollatorDSO;Extended Properties='Application=Windows';";

    [GeneratedRegex(@"^[`\@\＠\#\＃\＊\^,\&\＆/\\\$\%_;\[\]]+$")]
    private static partial Regex ReservedOnly();

    public async IAsyncEnumerable<SearchResultDto> SearchAsync(
        string query,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var sql = BuildQuery(query);
        if (sql is null) yield break;
        foreach (var result in await ExecuteAsync(sql, cancellationToken)) yield return result;
    }

    public static string? BuildQuery(string query)
    {
        var value = query.Trim();
        if (value.Length == 0 || ReservedOnly().IsMatch(value)) return null;
        value = value.Replace("'", "''", StringComparison.Ordinal);
        return "SELECT TOP 100 System.FileName, System.ItemUrl, System.ItemType FROM SystemIndex " +
            $"WHERE (System.FileName LIKE '{value}%' OR CONTAINS(System.FileName,'\"{value}*\"')) AND scope='file:' " +
            "ORDER BY System.Search.Rank DESC";
    }

    public static string ItemUrlToPath(string itemUrl) =>
        new Uri(itemUrl.Replace("#", "%23", StringComparison.Ordinal)).LocalPath;

    public static ResultKind ItemTypeToKind(string itemType) =>
        string.Equals(itemType, "Directory", StringComparison.Ordinal) ? ResultKind.Folder : ResultKind.File;

    private async Task<List<SearchResultDto>> ExecuteAsync(string sql, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new OleDbConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new OleDbCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var results = new List<SearchResultDto>();
            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.GetValue(0) is not string fileName ||
                    reader.GetValue(1) is not string itemUrl ||
                    reader.GetValue(2) is not string itemType ||
                    string.Equals(itemUrl, "file:", StringComparison.OrdinalIgnoreCase)) continue;
                var path = ItemUrlToPath(itemUrl);
                var kind = ItemTypeToKind(itemType);
                results.Add(new SearchResultDto
                {
                    Id = path,
                    ProviderId = FileSearchProvider.Id,
                    Title = fileName,
                    Subtitle = path,
                    Kind = kind,
                    Icon = new(kind == ResultKind.Folder ? IconSource.FileShellIcon : IconSource.FileThumbnail, path),
                    FilePath = path,
                    ExecutionToken = path,
                    CopyText = path,
                });
            }
            return results;
        }
        catch (OleDbException exception)
        {
            log?.Invoke($"INFO Windows Index unavailable: {exception.Message}");
            return [];
        }
    }
}
