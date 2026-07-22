// Selectively adapted from Beacon-old/Plugins/Flow.Launcher.Plugin.Explorer/Search/WindowsIndex/
// WindowsIndex.cs and QueryConstructor.cs (Flow Launcher/Wox, MIT).
using System.Data.OleDb;
using System.Text.RegularExpressions;
using Beacon.Contracts;

namespace Beacon.Platform.Windows;

public sealed partial class WindowsIndexSearch(Action<string>? log = null) : IDisposable
{
    public const string ConnectionString = "Provider=Search.CollatorDSO;Extended Properties='Application=Windows';";
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private OleDbConnection? _connection;

    [GeneratedRegex(@"^[`\@\＠\#\＃\＊\^,\&\＆/\\\$\%_;\[\]]+$")]
    private static partial Regex ReservedOnly();

    public async IAsyncEnumerable<SearchResultDto> SearchAsync(
        string query,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var sql = BuildQuery(query);
        if (sql is null) yield break;
        await foreach (var result in ExecuteAsync(sql, cancellationToken)) yield return result;
    }

    public static string? BuildQuery(string query)
    {
        var value = query.Trim();
        if (value.Length == 0 || ReservedOnly().IsMatch(value)) return null;
        var terms = Regex.Split(value, @"\s+")
            .Select(term => term.Replace("'", "''", StringComparison.Ordinal)
                .Replace("\"", "\"\"", StringComparison.Ordinal));
        var constraint = string.Join(" AND ", terms.Select(term =>
            $"(System.FileName LIKE '{term}%' OR CONTAINS(System.FileName,'\"{term}*\"'))"));
        return "SELECT TOP 100 System.FileName, System.ItemUrl, System.ItemType FROM SystemIndex " +
            $"WHERE {constraint} AND scope='file:' " +
            "ORDER BY System.Search.Rank DESC";
    }

    public static string ItemUrlToPath(string itemUrl) =>
        new Uri(itemUrl.Replace("#", "%23", StringComparison.Ordinal)).LocalPath;

    public static ResultKind ItemTypeToKind(string itemType) =>
        string.Equals(itemType, "Directory", StringComparison.Ordinal) ? ResultKind.Folder : ResultKind.File;

    private async IAsyncEnumerable<SearchResultDto> ExecuteAsync(
        string sql,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            OleDbCommand? command = null;
            System.Data.Common.DbDataReader? reader = null;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    _connection ??= new OleDbConnection(ConnectionString);
                    if (_connection.State != System.Data.ConnectionState.Open)
                        await _connection.OpenAsync(cancellationToken);
                    command = new OleDbCommand(sql, _connection);
                    reader = await command.ExecuteReaderAsync(cancellationToken);
                    break;
                }
                catch (OleDbException exception)
                {
                    command?.Dispose();
                    command = null;
                    ResetConnection();
                    if (attempt == 1) log?.Invoke($"INFO Windows Index unavailable: {exception.Message}");
                }
            }
            if (reader is null || command is null) yield break;
            await using (command)
            await using (reader)
            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.GetValue(0) is not string fileName ||
                    reader.GetValue(1) is not string itemUrl ||
                    reader.GetValue(2) is not string itemType ||
                    string.Equals(itemUrl, "file:", StringComparison.OrdinalIgnoreCase)) continue;
                var path = ItemUrlToPath(itemUrl);
                var kind = ItemTypeToKind(itemType);
                yield return new SearchResultDto
                {
                    Id = path,
                    ProviderId = FileSearchProvider.Id,
                    Title = fileName,
                    Subtitle = path,
                    Kind = kind,
                    Icon = ShellImageService.ForPath(path, kind == ResultKind.Folder),
                    FilePath = path,
                    ExecutionToken = path,
                    CopyText = path,
                };
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }


    public void Dispose()
    {
        ResetConnection();
        _connectionLock.Dispose();
    }

    private void ResetConnection()
    {
        _connection?.Dispose();
        _connection = null;
    }
}
