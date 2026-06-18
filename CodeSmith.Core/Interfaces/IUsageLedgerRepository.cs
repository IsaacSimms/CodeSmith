// == Usage Ledger Repository Interface == //
using CodeSmith.Core.Models.Usage;

namespace CodeSmith.Core.Interfaces;

public interface IUsageLedgerRepository
{
    Task AppendAsync(UsageLedgerEntry entry, CancellationToken ct = default);
}
