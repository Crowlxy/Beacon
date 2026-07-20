using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Beacon.Contracts;

namespace Beacon.Core;

public sealed record QuerySession(string SessionId, CancellationToken CancellationToken);

public sealed class QueryOrchestrator(IEnumerable<ISearchProvider> providers) : IDisposable
{
    private readonly ISearchProvider[] _providers = providers.ToArray();
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
            _sessionCancellation?.Dispose();
            _sessionCancellation = cancellation;
            _currentSession = session;
            _executionTokens = [];
        }

        var request = new SearchRequest(session.SessionId, rawQuery, scope, contractVersion);
        var channel = Channel.CreateUnbounded<SearchResultDto>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
        var producers = _providers
            .Select(provider => ProduceAsync(provider, request, channel.Writer, cancellation.Token))
            .ToArray();
        _ = CompleteAsync(producers, channel.Writer);

        while (await channel.Reader.WaitToReadAsync(CancellationToken.None))
        {
            while (channel.Reader.TryRead(out var result))
            {
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

    public void Dispose()
    {
        lock (_gate)
        {
            _sessionCancellation?.Cancel();
            _sessionCancellation?.Dispose();
            _sessionCancellation = null;
        }
    }

    private static async Task ProduceAsync(
        ISearchProvider provider,
        SearchRequest request,
        ChannelWriter<SearchResultDto> writer,
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
    }

    private static async Task CompleteAsync(Task[] producers, ChannelWriter<SearchResultDto> writer)
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
    }
}
