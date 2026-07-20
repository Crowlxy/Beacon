namespace Beacon.Contracts;

public static class ContractVersion
{
    public const int Current = 2;

    public static bool TryValidate(int version, out string? failureReason)
    {
        failureReason = version == Current ? null : $"Unsupported contract version: {version}.";
        return failureReason is null;
    }
}

public enum QueryScope { All, Applications, Files, Folders, Settings, Actions, Calculator, Url, WebSearch }
public enum ResultKind { Unknown = 0, Application, File, Folder, Setting, Action, Calculation, Url, WebSearch, Plugin }
public enum IconSource { None, FilePath, FileShellIcon, FileThumbnail, UriOrDataUri, FluentGlyph, ProviderIcon }

public sealed record SearchRequest(string SessionId, string RawQuery, QueryScope Scope, int ContractVersion);

public sealed record SearchResultDto
{
    public required string Id { get; init; }
    public required string ProviderId { get; init; }
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public ResultKind Kind { get; init; }
    public double Score { get; init; }
    public IconDescriptor Icon { get; init; } = new(IconSource.None, null);
    public string? ExecutionToken { get; init; }
    public string? CopyText { get; init; }
    public string? AutoCompleteText { get; init; }
    public string? FilePath { get; init; }
}

public sealed record IconDescriptor(IconSource Source, string? Value);
public sealed record ExecuteRequest(string SessionId, string ResultId, string ExecutionToken, int ContractVersion);
public sealed record ExecuteResponse(bool Success, string? FailureReason);

public interface ISearchProvider
{
    string ProviderId { get; }
    IAsyncEnumerable<SearchResultDto> SearchAsync(SearchRequest request, CancellationToken cancellationToken);
}
