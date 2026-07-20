using System.Runtime.CompilerServices;
using System.Text.Json;
using Beacon.Contracts;
using Beacon.Core;
using NUnit.Framework;

namespace Beacon.Core.Tests;

public sealed class ContractSerializationTests
{
    [Test]
    public void AllDtosAndEnumsRoundTrip()
    {
        AssertRoundTrip(new SearchRequest("session", "query", QueryScope.Files, ContractVersion.Current));
        AssertRoundTrip(new SearchResultDto
        {
            Id = "id",
            ProviderId = "provider",
            Title = "title",
            Subtitle = null,
            Kind = ResultKind.File,
            Score = 12.5,
            Icon = new(IconSource.FileShellIcon, @"C:\file.txt"),
            ExecutionToken = "token",
            CopyText = null,
            AutoCompleteText = "auto",
            FilePath = @"C:\file.txt",
        });
        AssertRoundTrip(new IconDescriptor(IconSource.None, null));
        AssertRoundTrip(new ExecuteRequest("session", "id", "token", ContractVersion.Current));
        AssertRoundTrip(new ExecuteResponse(false, null));
        AssertRoundTrip(QueryScope.WebSearch);
        AssertRoundTrip(ResultKind.Plugin);
        AssertRoundTrip(IconSource.UriOrDataUri);
    }

    private static void AssertRoundTrip<T>(T value)
    {
        var actual = JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value));
        Assert.That(actual, Is.EqualTo(value));
    }
}

public sealed class QueryOrchestratorTests
{
    [Test]
    public async Task ResultsAreMergedInArrivalOrder()
    {
        using var orchestrator = new QueryOrchestrator([
            new DummyProvider("slow", 80),
            new DummyProvider("fast", 10),
        ]);

        var results = await CollectAsync(orchestrator.SearchAsync("query"));

        Assert.That(results[0].ProviderId, Is.EqualTo("fast"));
        Assert.That(results[1].ProviderId, Is.EqualTo("slow"));
    }

    [Test]
    public async Task NewQueryCancelsAndSuppressesPreviousSession()
    {
        var provider = new ControlledProvider();
        using var orchestrator = new QueryOrchestrator([provider]);
        await using var oldResults = orchestrator.SearchAsync("old").GetAsyncEnumerator();
        var oldMove = oldResults.MoveNextAsync().AsTask();
        var oldToken = await provider.FirstToken.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var currentResults = CollectAsync(orchestrator.SearchAsync("new"));
        await provider.SecondStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        provider.Release.TrySetResult();

        Assert.That(oldToken.IsCancellationRequested, Is.True);
        Assert.That(await oldMove, Is.False);
        Assert.That((await currentResults).Single().Title, Is.EqualTo("new"));
    }

    [Test]
    public async Task ExecuteRequestAcceptsOnlyCurrentKnownTokenAndVersion()
    {
        using var orchestrator = new QueryOrchestrator([new DummyProvider("provider", 0, "token")]);
        var result = (await CollectAsync(orchestrator.SearchAsync("query"))).Single();
        var sessionId = orchestrator.CurrentSession!.SessionId;

        Assert.That(orchestrator.ValidateExecuteRequest(new(sessionId, result.Id, "token", ContractVersion.Current)).Success, Is.True);
        Assert.That(orchestrator.ValidateExecuteRequest(new("old", result.Id, "token", ContractVersion.Current)).Success, Is.False);
        Assert.That(orchestrator.ValidateExecuteRequest(new(sessionId, result.Id, "unknown", ContractVersion.Current)).Success, Is.False);
        Assert.That(orchestrator.ValidateExecuteRequest(new(sessionId, result.Id, "token", ContractVersion.Current - 1)).Success, Is.False);
    }

    [Test]
    public async Task UnsupportedSearchVersionIsIgnored()
    {
        using var orchestrator = new QueryOrchestrator([new DummyProvider("provider", 0)]);
        Assert.That(await CollectAsync(orchestrator.SearchAsync("query", contractVersion: 1)), Is.Empty);
    }

    private static async Task<List<SearchResultDto>> CollectAsync(IAsyncEnumerable<SearchResultDto> source)
    {
        var results = new List<SearchResultDto>();
        await foreach (var result in source)
        {
            results.Add(result);
        }
        return results;
    }

    private sealed class DummyProvider(string providerId, int delayMilliseconds, string? token = null) : ISearchProvider
    {
        public string ProviderId => providerId;

        public async IAsyncEnumerable<SearchResultDto> SearchAsync(
            SearchRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Delay(delayMilliseconds, cancellationToken);
            yield return Result(ProviderId, request.RawQuery, token);
        }
    }

    private sealed class ControlledProvider : ISearchProvider
    {
        private int _calls;
        public string ProviderId => "controlled";
        public TaskCompletionSource<CancellationToken> FirstToken { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async IAsyncEnumerable<SearchResultDto> SearchAsync(
            SearchRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _calls) == 1)
            {
                FirstToken.TrySetResult(cancellationToken);
            }
            else
            {
                SecondStarted.TrySetResult();
            }

            await Release.Task;
            yield return Result(ProviderId, request.RawQuery);
        }
    }

    private static SearchResultDto Result(string providerId, string title, string? token = null) => new()
    {
        Id = $"{providerId}:{title}",
        ProviderId = providerId,
        Title = title,
        ExecutionToken = token,
    };
}
