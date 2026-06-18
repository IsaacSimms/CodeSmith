// == Credit Balance Repository Interface == //
using CodeSmith.Core.Models.Usage;

namespace CodeSmith.Core.Interfaces;

public interface ICreditBalanceRepository
{
    Task<CreditBalance?> GetAsync(string objectId, CancellationToken ct = default);

    Task SaveAsync(CreditBalance balance, CancellationToken ct = default);
}
