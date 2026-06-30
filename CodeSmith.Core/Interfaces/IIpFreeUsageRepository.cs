// == IP Free Usage Repository Interface == //
namespace CodeSmith.Core.Interfaces;

public interface IIpFreeUsageRepository
{
    Task<long> GetIssuedAsync(string ip, CancellationToken ct = default);

    // Adjusts the issued total for the IP by a signed delta. Positive grants free tokens (creating the
    // row if missing); negative refunds a prior hold (floored at zero, never creating a row). Zero is a
    // no-op. Refund support is what lets a reserved IP hold be reversed on settle/release.
    Task AddIssuedAsync(string ip, long amount, CancellationToken ct = default);
}