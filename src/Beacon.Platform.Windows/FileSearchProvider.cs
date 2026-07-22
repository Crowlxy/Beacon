using System.Runtime.CompilerServices;
using Beacon.Contracts;
using Beacon.Platform.Windows.Everything;

namespace Beacon.Platform.Windows;

public sealed class FileSearchProvider(Action<string>? log = null) : ISearchProvider, IDisposable
{
    private readonly WindowsIndexSearch _windowsIndex = new(log);
    public const string Id = "windows.files";
    public string ProviderId => Id;

    public async IAsyncEnumerable<SearchResultDto> SearchAsync(
        SearchRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (request.Scope is not (QueryScope.All or QueryScope.Files or QueryScope.Folders) ||
            string.IsNullOrWhiteSpace(request.RawQuery)) yield break;

        var availability = await EverythingApi.GetAvailabilityAsync(cancellationToken);
        if (availability.Available) log?.Invoke("INFO File search route: Everything");
        var search = availability.Available
            ? new EverythingSearchManager(log).SearchAsync(request.RawQuery, availabilityConfirmed: true, cancellationToken: cancellationToken)
            : _windowsIndex.SearchAsync(request.RawQuery, cancellationToken);
        if (!availability.Available) log?.Invoke($"INFO {availability.Reason} Using Windows Index.");
        await foreach (var result in search)
        {
            if (request.Scope == QueryScope.Files && result.Kind != ResultKind.File) continue;
            if (request.Scope == QueryScope.Folders && result.Kind != ResultKind.Folder) continue;
            yield return result;
        }
    }


    public void Dispose() => _windowsIndex.Dispose();
}

public static class WindowsRecentFiles
{
    public static IReadOnlyList<SearchResultDto> Get(int maximumCount = 8)
    {
        var recent = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
        if (!Directory.Exists(recent)) return [];
        var results = new List<SearchResultDto>();
        foreach (var shortcut in Directory.EnumerateFiles(recent, "*.lnk").OrderByDescending(File.GetLastWriteTimeUtc))
        {
            try
            {
                var target = Programs.ShellLinkReader.Read(shortcut).TargetPath;
                if (string.IsNullOrWhiteSpace(target) || !File.Exists(target)) continue;
                results.Add(new SearchResultDto
                {
                    Id = target,
                    ProviderId = FileSearchProvider.Id,
                    Title = Path.GetFileName(target),
                    Subtitle = target,
                    Kind = ResultKind.File,
                    Icon = new(IconSource.FileShellIcon, target),
                    FilePath = target,
                    ExecutionToken = target,
                    AutoCompleteText = Path.GetFileName(target),
                });
                if (results.Count == maximumCount) break;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
        return results;
    }
}
