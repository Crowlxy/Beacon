using System.Runtime.CompilerServices;
using Beacon.Contracts;
using Beacon.Platform.Windows.Everything;

namespace Beacon.Platform.Windows;

public sealed class FileSearchProvider(Action<string>? log = null) : ISearchProvider
{
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
            : new WindowsIndexSearch(log).SearchAsync(request.RawQuery, cancellationToken);
        if (!availability.Available) log?.Invoke($"INFO {availability.Reason} Using Windows Index.");
        await foreach (var result in search)
        {
            if (request.Scope == QueryScope.Files && result.Kind != ResultKind.File) continue;
            if (request.Scope == QueryScope.Folders && result.Kind != ResultKind.Folder) continue;
            yield return result;
        }
    }
}
