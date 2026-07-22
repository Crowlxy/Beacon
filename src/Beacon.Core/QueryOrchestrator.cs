using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Beacon.Contracts;

namespace Beacon.Core;

public sealed record QuerySession(string SessionId, CancellationToken CancellationToken);

public sealed class QueryOrchestrator(
    IEnumerable<ISearchProvider> providers,
    UsageHistoryStore? history = null,
    TimeSpan? providerTimeout = null,
    Action<string>? log = null) : IDisposable
{
    private readonly ISearchProvider[] _providers = providers.ToArray();
    private readonly TimeSpan _providerTimeout = providerTimeout ?? TimeSpan.FromSeconds(2);
    private readonly object _gate = new();
    private CancellationTokenSource? _sessionCancellation;
    private QuerySession? _currentSession;
    private Dictionary<string, string> _executionTokens = [];

    public QuerySession? CurrentSession
    {
        get { lock (_gate) return _currentSession; }
    }

    public async IAsyncEnumerable<SearchResultDto> SearchAsync(
        string rawQuery,
        QueryScope scope = QueryScope.All,
        int contractVersion = ContractVersion.Current,
        RankingContext? rankingContext = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!ContractVersion.TryValidate(contractVersion, out _))
        {
            yield break;
        }

        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var session = new QuerySession(Guid.NewGuid().ToString("N"), cancellation.Token);
        lock (_gate)
        {
            _sessionCancellation?.Cancel();
            _sessionCancellation = cancellation;
            _currentSession = session;
            _executionTokens = [];
        }

        var request = new SearchRequest(session.SessionId, rawQuery, scope, contractVersion);
        var context = rankingContext ?? new RankingContext(DateTimeOffset.Now);
        var channel = Channel.CreateUnbounded<SearchResultDto>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
        var producers = _providers
            .Select(provider => Task.Run(
                () => ProduceWithinDeadlineAsync(provider, request, channel.Writer, cancellation.Token),
                CancellationToken.None))
            .ToArray();
        _ = CompleteAsync(producers, channel.Writer, cancellation);

        while (await channel.Reader.WaitToReadAsync(CancellationToken.None))
        {
            while (channel.Reader.TryRead(out var result))
            {
                result = result with
                {
                    Score = RankingEngine.Score(result, rawQuery, history?.Get(result.Id), history?.MaximumSelectionCount ?? 0, context),
                };
                lock (_gate)
                {
                    if (_currentSession != session)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(result.ExecutionToken))
                    {
                        _executionTokens.TryAdd(result.Id, result.ExecutionToken);
                    }
                }

                yield return result;
            }
        }
    }

    public ExecuteResponse ValidateExecuteRequest(ExecuteRequest? request)
    {
        if (request is null)
        {
            return new(false, "The execute request is missing.");
        }

        if (!ContractVersion.TryValidate(request.ContractVersion, out var reason))
        {
            return new(false, reason);
        }

        lock (_gate)
        {
            if (_currentSession?.SessionId != request.SessionId)
            {
                return new(false, "The query session is no longer current.");
            }

            return _executionTokens.TryGetValue(request.ResultId, out var token) && token == request.ExecutionToken
                ? new(true, null)
                : new(false, "The result or execution token is invalid.");
        }
    }

    public void Cancel()
    {
        lock (_gate)
        {
            _sessionCancellation?.Cancel();
            _currentSession = null;
            _executionTokens = [];
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _sessionCancellation?.Cancel();
            _sessionCancellation = null;
        }
    }

    private static async Task ProduceAsync(
        ISearchProvider provider,
        SearchRequest request,
        ChannelWriter<SearchResultDto> writer,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var result in provider.SearchAsync(request, cancellationToken).WithCancellation(cancellationToken))
            {
                if (result.ProviderId == provider.ProviderId &&
                    !string.IsNullOrWhiteSpace(result.Id) &&
                    !string.IsNullOrWhiteSpace(result.Title))
                {
                    await writer.WriteAsync(result, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            log?.Invoke($"WARN Provider {provider.ProviderId} failed: {exception.Message}");
        }
    }

    private async Task ProduceWithinDeadlineAsync(
        ISearchProvider provider,
        SearchRequest request,
        ChannelWriter<SearchResultDto> writer,
        CancellationToken sessionCancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(sessionCancellation);
        var producer = ProduceAsync(provider, request, writer, log, deadline.Token);
        var completed = await Task.WhenAny(producer, Task.Delay(_providerTimeout, sessionCancellation));
        if (completed == producer)
        {
            await producer;
            return;
        }

        deadline.Cancel();
        log?.Invoke($"WARN Provider {provider.ProviderId} exceeded {_providerTimeout.TotalMilliseconds:F0}ms deadline");
        _ = producer.ContinueWith(static task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);
    }

    private async Task CompleteAsync(
        Task[] producers,
        ChannelWriter<SearchResultDto> writer,
        CancellationTokenSource cancellation)
    {
        try
        {
            await Task.WhenAll(producers);
            writer.TryComplete();
        }
        catch (Exception exception)
        {
            writer.TryComplete(exception);
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_sessionCancellation, cancellation)) _sessionCancellation = null;
            }
            cancellation.Dispose();
        }
    }
}
