// == IP Free Usage Repository Interface == //
namespace CodeSmith.Core.Interfaces;

public interface IIpFreeUsageRepository
{
    Task<long> GetIssuedAsync(string ip, CancellationToken ct = default);

    /// <summary>
    /// Adds the given amount to the issued total for the IP. Creates row if missing.
    /// </summary>
    Task AddIssuedAsync(string ip, long amount, CancellationToken ct = default);
}