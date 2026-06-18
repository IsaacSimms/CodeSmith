// == EF Credit Balance Repository == //
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models.Usage;
using Microsoft.EntityFrameworkCore;

namespace CodeSmith.Infrastructure.Persistence.Repositories;

public class EfCreditBalanceRepository : ICreditBalanceRepository
{
    private readonly CodeSmithDbContext _db;

    public EfCreditBalanceRepository(CodeSmithDbContext db)
    {
        _db = db;
    }

    public async Task<CreditBalance?> GetAsync(string objectId, CancellationToken ct = default)
    {
        return await _db.CreditBalances
            .FirstOrDefaultAsync(b => b.ObjectId == objectId, ct);
    }

    public async Task SaveAsync(CreditBalance balance, CancellationToken ct = default)
    {
        if (_db.Entry(balance).State == EntityState.Detached)
        {
            var existing = await _db.CreditBalances.FindAsync(new object[] { balance.ObjectId }, ct);
            if (existing is null)
            {
                _db.CreditBalances.Add(balance);
            }
            else
            {
                _db.Entry(existing).CurrentValues.SetValues(balance);
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
