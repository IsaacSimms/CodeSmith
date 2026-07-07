// == EF Usage Ledger Repository == //
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models.Usage;
using Microsoft.EntityFrameworkCore;

namespace CodeSmith.Infrastructure.Persistence.Repositories;

public class EfUsageLedgerRepository : IUsageLedgerRepository
{
    private readonly CodeSmithDbContext _db;

    public EfUsageLedgerRepository(CodeSmithDbContext db)
    {
        _db = db;
    }

    public async Task AppendAsync(UsageLedgerEntry entry, CancellationToken ct = default)
    {
        _db.UsageLedgerEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<UsageLedgerEntry>> GetRecentAsync(string objectId, int take, CancellationToken ct = default)
    {
        return await _db.UsageLedgerEntries
            .Where(e => e.ObjectId == objectId)
            .OrderByDescending(e => e.TimestampUtc)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(ct);
    }
}
