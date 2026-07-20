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
    [JsonRpcMethod("search", UseSingleObjectParameterDeserialization = true)]
    public async Task SearchAsync(SearchRequest? request, CancellationToken cancellationToken)
    {
        if (request is null ||
            !ContractVersion.TryValidate(request.ContractVersion, out _) ||
            string.IsNullOrWhiteSpace(request.SessionId) ||
            request.SessionId.Length > 128 ||
            request.RawQuery is null ||
            request.RawQuery.Length > 4096 ||
            !Enum.IsDefined(request.Scope))
        {
            return;
        }

        for (var index = 1; index <= 5; index++)
        {
            await Task.Delay(150, cancellationToken);
            var result = new SearchResultDto
            {
                Id = $"{request.SessionId}:{index}",
                ProviderId = "r1-dummy",
                Title = $"{request.RawQuery} result {index}",
                Kind = ResultKind.Plugin,
                Score = 100 - index,
                Icon = new(IconSource.ProviderIcon, "r1-dummy"),
                ExecutionToken = $"{request.SessionId}:execute:{index}",
            };
            await rpc.NotifyWithParameterObjectAsync("searchResult", result);
        }
    }
}
