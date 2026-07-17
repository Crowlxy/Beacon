namespace Beacon.Contracts;

public static class ContractVersion
{
    public const int Current = 1;
}

public sealed record SearchRequest(string SessionId, string RawQuery, int ContractVersion);

public sealed record SearchResultDto(
    string Id,
    string ProviderId,
    string Title,
    double Score,
    int ContractVersion);
