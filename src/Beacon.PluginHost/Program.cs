using System.IO.Pipes;
using Beacon.Contracts;
using StreamJsonRpc;

return await PluginHostProgram.RunAsync(args);

internal static class PluginHostProgram
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (!TryGetRequiredArgument(args, "--pipe", out var pipeName) ||
            !TryGetRequiredArgument(args, "--data-root", out var dataRoot) ||
            !Path.IsPathFullyQualified(dataRoot))
        {
            Console.Error.WriteLine("Usage: Beacon.PluginHost --pipe <name> --data-root <absolute-path>");
            return 2;
        }

        Directory.CreateDirectory(dataRoot);
        await using var pipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.WaitForConnectionAsync();

        using var rpc = new JsonRpc(new HeaderDelimitedMessageHandler(pipe, pipe, new SystemTextJsonFormatter()));
        rpc.AddLocalRpcTarget(new SearchRpc(rpc));
        rpc.StartListening();
        await rpc.Completion;
        return 0;
    }

    private static bool TryGetRequiredArgument(string[] args, string name, out string value)
    {
        var index = Array.IndexOf(args, name);
        value = index >= 0 && index + 1 < args.Length ? args[index + 1] : string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }
}

internal sealed class SearchRpc(JsonRpc rpc)
{
    [JsonRpcMethod("search")]
    public async Task SearchAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ContractVersion != ContractVersion.Current ||
            string.IsNullOrWhiteSpace(request.SessionId) ||
            request.SessionId.Length > 128 ||
            request.RawQuery.Length > 4096)
        {
            throw new ArgumentException("Invalid search request.", nameof(request));
        }

        for (var index = 1; index <= 5; index++)
        {
            await Task.Delay(150, cancellationToken);
            var result = new SearchResultDto(
                $"{request.SessionId}:{index}",
                "r1-dummy",
                $"{request.RawQuery} result {index}",
                100 - index,
                ContractVersion.Current);
            await rpc.NotifyWithParameterObjectAsync("searchResult", result);
        }
    }
}
