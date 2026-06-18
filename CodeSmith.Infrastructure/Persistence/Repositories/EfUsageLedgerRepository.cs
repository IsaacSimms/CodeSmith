// == EF Usage Ledger Repository == //
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models.Usage;

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
}
