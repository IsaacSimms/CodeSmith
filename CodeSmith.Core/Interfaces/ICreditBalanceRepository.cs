// == Credit Balance Repository Interface == //
using CodeSmith.Core.Models.Usage;

namespace CodeSmith.Core.Interfaces;

public interface ICreditBalanceRepository
{
    Task<CreditBalance?> GetAsync(string objectId, CancellationToken ct = default);

    // Returns the existing balance or a new one seeded with canonical defaults (free quota + window start).
    // Single source of truth for balance creation, shared by usage enforcement and billing top-ups.
    Task<CreditBalance> GetOrCreateAsync(string objectId, long freeQuotaMax, CancellationToken ct = default);

    Task SaveAsync(CreditBalance balance, CancellationToken ct = default);
}
