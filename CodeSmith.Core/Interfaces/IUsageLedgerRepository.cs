// == Usage Ledger Repository Interface == //
using CodeSmith.Core.Models.Usage;

namespace CodeSmith.Core.Interfaces;

public interface IUsageLedgerRepository
{
    Task AppendAsync(UsageLedgerEntry entry, CancellationToken ct = default);

    // Most-recent-first slice of a user's ledger (top-ups and spends), for the billing read endpoint.
    Task<IReadOnlyList<UsageLedgerEntry>> GetRecentAsync(string objectId, int take, CancellationToken ct = default);
}
