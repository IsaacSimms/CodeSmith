// == Usage Store Interface == //
using CodeSmith.Core.Models.Usage;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// The usage-enforcement storage seam. One deep Interface speaking enforcement's language:
/// read the full decision state (credit balance + per-IP free-token aggregate) in one call, then
/// persist a phase's outcome — the mutated balance, an IP-aggregate delta, and optionally a ledger
/// entry — as ONE unit of work (a single SaveChanges). Replaces the enforcer's composition of three
/// shallow repositories, each of which issued its own SaveChanges, so an enforcement phase costs at
/// most two read round-trips plus one write instead of four to seven.
/// </summary>
public interface IUsageStore
{
    Task<UsageSnapshot> GetSnapshotAsync(string objectId, string clientIp, CancellationToken ct = default);

    // balance null = no balance row to write (release for a user with no persisted balance — must
    // not mint one); ipIssuedDelta may be negative (refund; floored at zero; never creates a row)
    Task PersistAsync(CreditBalance? balance, string clientIp, long ipIssuedDelta, UsageLedgerEntry? ledgerEntry = null, CancellationToken ct = default);
}
