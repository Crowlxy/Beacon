using System.Diagnostics;
using Beacon.Contracts;
using Beacon.Platform.Windows;
using NUnit.Framework;

namespace Beacon.Platform.Windows.Tests;

public sealed class StandardProviderTests
{
    [Test]
    public async Task WindowsSettingsUsesMigratedJapaneseResources()
    {
        var results = await Results(new WindowsSettingsProvider(), "ディスプレイ");
        Assert.That(results, Has.Some.Property(nameof(SearchResultDto.ExecutionToken)).StartsWith("ms-settings:"));
    }

    [Test]
    public async Task ShellRequiresPrefixAndReturnsCommand()
    {
        Assert.That(await Results(new ShellSearchProvider(), "echo ok"), Is.Empty);
        Assert.That((await Results(new ShellSearchProvider(), "> echo ok")).Single().ExecutionToken, Is.EqualTo("echo ok"));
    }

    [Test]
    public async Task SystemActionsMatchJapaneseAndRespectCancellation()
    {
        Assert.That((await Results(new SystemActionSearchProvider(), "再起動")).Single().ExecutionToken, Is.EqualTo("restart"));
        Assert.That(SystemActionSearchProvider.Browse(), Has.Count.EqualTo(5));
        Assert.That(SystemActionSearchProvider.Browse("ロック").Single().ExecutionToken, Is.EqualTo("lock"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in new SystemActionSearchProvider().SearchAsync(Request("再起動"), cancellation.Token)) { }
        });
    }

    [Test]
    public async Task ProcessKillerFindsCurrentProcessWithoutTerminatingIt()
    {
        using var current = Process.GetCurrentProcess();
        var results = await Results(new ProcessKillerSearchProvider(), "kill " + current.ProcessName);
        Assert.That(results, Has.Some.Property(nameof(SearchResultDto.ExecutionToken)).EqualTo(current.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Test]
    public void ChromiumLoaderTraversesNestedFolders()
    {
        var path = Path.Combine(Path.GetTempPath(), $"Beacon-bookmarks-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{\"roots\":{\"bookmark_bar\":{\"children\":[{\"type\":\"folder\",\"children\":[{\"type\":\"url\",\"name\":\"Beacon\",\"url\":\"https://example.com\"}]}]}}}");
            Assert.That(BookmarkLoader.LoadChromiumFile(path, "Test").Single(), Is.EqualTo(new Bookmark("Beacon", "https://example.com", "Test")));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static SearchRequest Request(string query) => new("session", query, QueryScope.All, ContractVersion.Current);

    private static async Task<List<SearchResultDto>> Results(ISearchProvider provider, string query)
    {
        var results = new List<SearchResultDto>();
        await foreach (var result in provider.SearchAsync(Request(query), CancellationToken.None)) results.Add(result);
        return results;
    }
}
