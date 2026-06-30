// == EF IP Free Usage Repository == //
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models.Usage;
using Microsoft.EntityFrameworkCore;

namespace CodeSmith.Infrastructure.Persistence.Repositories;

public class EfIpFreeUsageRepository : IIpFreeUsageRepository
{
    private readonly CodeSmithDbContext _db;

    public EfIpFreeUsageRepository(CodeSmithDbContext db)
    {
        _db = db;
    }

    public async Task<long> GetIssuedAsync(string ip, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ip)) return 0;

        var record = await _db.IpFreeUsages
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Ip == ip, ct);

        return record?.FreeTokensIssued ?? 0;
    }

    public async Task AddIssuedAsync(string ip, long amount, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ip) || amount == 0) return;

        var record = await _db.IpFreeUsages.FindAsync(new object[] { ip }, ct);
        if (record is null)
        {
            // Nothing to refund against a missing row; only a positive grant creates one.
            if (amount < 0) return;
            record = new IpFreeUsage { Ip = ip, FreeTokensIssued = amount, FirstSeenUtc = DateTime.UtcNow };
            _db.IpFreeUsages.Add(record);
        }
        else
        {
            record.FreeTokensIssued = Math.Max(0, record.FreeTokensIssued + amount); // floor refunds at zero
        }

        await _db.SaveChangesAsync(ct);
    }
}