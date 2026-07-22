// Adapted from Beacon-old/Plugins/Flow.Launcher.Plugin.Explorer/Search/Everything/EverythingSearchManager.cs (Flow Launcher/Wox, MIT).
using System.Runtime.CompilerServices;
using Beacon.Contracts;

namespace Beacon.Platform.Windows.Everything;

public sealed class EverythingSearchManager(Action<string>? log = null)
{
    public async IAsyncEnumerable<SearchResultDto> SearchAsync(
        string query,
        int maxCount = 100,
        bool availabilityConfirmed = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!availabilityConfirmed)
        {
            var availability = await EverythingApi.GetAvailabilityAsync(cancellationToken);
            if (!availability.Available)
            {
                log?.Invoke(availability.Reason!);
                yield break;
            }
        }

        await foreach (var item in EverythingApi.SearchAsync(
            new EverythingSearchOption(query, MaxCount: maxCount), cancellationToken))
        {
            yield return new SearchResultDto
            {
                Id = item.Path,
                ProviderId = FileSearchProvider.Id,
                Title = Path.GetFileName(item.Path.TrimEnd(Path.DirectorySeparatorChar)),
                Subtitle = item.Path,
                Kind = item.IsFolder ? ResultKind.Folder : ResultKind.File,
                Icon = ShellImageService.ForPath(item.Path, item.IsFolder),
                FilePath = item.Path,
                CopyText = item.Path,
                ExecutionToken = item.Path,
            };
        }
    }
}
