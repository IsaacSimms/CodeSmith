// == EF Usage Store == //
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models.Usage;
using Microsoft.EntityFrameworkCore;

namespace CodeSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF adapter of the usage-store seam. GetSnapshotAsync reads the balance and the IP aggregate as
/// tracked entities (two SELECTs); PersistAsync lands the whole phase outcome — balance write,
/// IP-aggregate delta, optional ledger append — in ONE SaveChangesAsync, so an enforcement phase
/// costs at most three database round-trips instead of the four to seven the per-repository
/// SaveChanges pattern produced. Tracked snapshot entities make PersistAsync's FindAsync calls
/// identity-map hits, not extra queries.
/// </summary>
public class EfUsageStore : IUsageStore
{
    private readonly CodeSmithDbContext _db;

    public EfUsageStore(CodeSmithDbContext db)
    {
        _db = db;
    }

    // == GetSnapshotAsync == //

    public async Task<UsageSnapshot> GetSnapshotAsync(string objectId, string clientIp, CancellationToken ct = default)
    {
        var balance = await _db.CreditBalances
            .FirstOrDefaultAsync(b => b.ObjectId == objectId, ct);

        long ipIssued = 0;
        if (!string.IsNullOrWhiteSpace(clientIp))
        {
            var record = await _db.IpFreeUsages.FindAsync(new object[] { clientIp }, ct);
            ipIssued = record?.FreeTokensIssued ?? 0;
        }

        return new UsageSnapshot(balance, ipIssued);
    }

    // == PersistAsync == //

    public async Task PersistAsync(CreditBalance? balance, string clientIp, long ipIssuedDelta, UsageLedgerEntry? ledgerEntry = null, CancellationToken ct = default)
    {
        // Attach-or-insert the balance; a snapshot-tracked instance is already known to the context
        if (balance is not null && _db.Entry(balance).State == EntityState.Detached)
        {
            var existing = await _db.CreditBalances.FindAsync(new object[] { balance.ObjectId }, ct);
            if (existing is null)
                _db.CreditBalances.Add(balance);
            else
                _db.Entry(existing).CurrentValues.SetValues(balance);
        }

        if (ipIssuedDelta != 0 && !string.IsNullOrWhiteSpace(clientIp))
        {
            var record = await _db.IpFreeUsages.FindAsync(new object[] { clientIp }, ct);
            if (record is null)
            {
                // Nothing to refund against a missing row; only a positive grant creates one.
                if (ipIssuedDelta > 0)
                    _db.IpFreeUsages.Add(new IpFreeUsage { Ip = clientIp, FreeTokensIssued = ipIssuedDelta, FirstSeenUtc = DateTime.UtcNow });
            }
            else
            {
                record.FreeTokensIssued = Math.Max(0, record.FreeTokensIssued + ipIssuedDelta); // floor refunds at zero
            }
        }

        if (ledgerEntry is not null)
            _db.UsageLedgerEntries.Add(ledgerEntry);

        await _db.SaveChangesAsync(ct);
    }
}
