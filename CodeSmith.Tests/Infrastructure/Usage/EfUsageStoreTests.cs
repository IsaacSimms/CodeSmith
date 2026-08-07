// == EF Usage Store Tests == //
using CodeSmith.Core.Models.Usage;
using CodeSmith.Infrastructure.Persistence;
using CodeSmith.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CodeSmith.Tests.Infrastructure.Usage;

/// <summary>
/// Pins the EF adapter of the usage-store seam: snapshot reads, attach-or-insert balance writes,
/// IP-aggregate delta semantics (create on grant, floor on refund, never create on refund), and the
/// load-bearing perf invariant that one PersistAsync is ONE SaveChanges regardless of what it carries.
/// </summary>
public class EfUsageStoreTests
{
    private const string ObjectId = "user-1";
    private const string ClientIp = "1.2.3.4";

    // == Helpers == //

    // Counts SaveChangesAsync executions so tests can pin the single-unit-of-work invariant
    private sealed class SaveCounter : SaveChangesInterceptor
    {
        public int Count { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Count++;
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private static (CodeSmithDbContext Db, SaveCounter Saves) NewContext()
    {
        var counter = new SaveCounter();
        var options = new DbContextOptionsBuilder<CodeSmithDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .AddInterceptors(counter)
            .Options;
        return (new CodeSmithDbContext(options), counter);
    }

    private static CreditBalance Balance(long freeUsed = 0, decimal paid = 0m) => new()
    {
        ObjectId           = ObjectId,
        FreeQuotaMax       = 20_000,
        FreeTokensUsed     = freeUsed,
        PaidCreditsBalance = paid
    };

    // == GetSnapshotAsync == //

    [Fact]
    public async Task GetSnapshotAsync_NoRows_ReturnsNullBalanceAndZeroIpIssued()
    {
        var (db, _) = NewContext();
        using (db)
        {
            var snapshot = await new EfUsageStore(db).GetSnapshotAsync(ObjectId, ClientIp);

            Assert.Null(snapshot.Balance);
            Assert.Equal(0, snapshot.IpFreeTokensIssued);
        }
    }

    [Fact]
    public async Task GetSnapshotAsync_ReadsBalanceAndIpAggregateTogether()
    {
        var (db, _) = NewContext();
        using (db)
        {
            db.CreditBalances.Add(Balance(freeUsed: 500, paid: 3m));
            db.IpFreeUsages.Add(new IpFreeUsage { Ip = ClientIp, FreeTokensIssued = 1234, FirstSeenUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();

            var snapshot = await new EfUsageStore(db).GetSnapshotAsync(ObjectId, ClientIp);

            Assert.NotNull(snapshot.Balance);
            Assert.Equal(500,  snapshot.Balance!.FreeTokensUsed);
            Assert.Equal(3m,   snapshot.Balance.PaidCreditsBalance);
            Assert.Equal(1234, snapshot.IpFreeTokensIssued);
        }
    }

    // == PersistAsync: balance == //

    [Fact]
    public async Task PersistAsync_DetachedNewBalance_InsertsRow()
    {
        var (db, _) = NewContext();
        using (db)
        {
            await new EfUsageStore(db).PersistAsync(Balance(freeUsed: 100), ClientIp, ipIssuedDelta: 0);

            var stored = await db.CreditBalances.SingleAsync();
            Assert.Equal(100, stored.FreeTokensUsed);
        }
    }

    [Fact]
    public async Task PersistAsync_TrackedBalanceMutation_UpdatesRow()
    {
        var (db, _) = NewContext();
        using (db)
        {
            db.CreditBalances.Add(Balance(paid: 10m));
            await db.SaveChangesAsync();

            var store    = new EfUsageStore(db);
            var snapshot = await store.GetSnapshotAsync(ObjectId, ClientIp);
            snapshot.Balance!.PaidCreditsBalance = 7m;

            await store.PersistAsync(snapshot.Balance, ClientIp, ipIssuedDelta: 0);

            Assert.Equal(7m, (await db.CreditBalances.SingleAsync()).PaidCreditsBalance);
        }
    }

    [Fact]
    public async Task PersistAsync_NullBalance_WritesOnlyTheIpDelta()
    {
        var (db, _) = NewContext();
        using (db)
        {
            db.IpFreeUsages.Add(new IpFreeUsage { Ip = ClientIp, FreeTokensIssued = 300, FirstSeenUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();

            await new EfUsageStore(db).PersistAsync(balance: null, ClientIp, ipIssuedDelta: -200);

            Assert.Empty(db.CreditBalances);
            Assert.Equal(100, (await db.IpFreeUsages.SingleAsync()).FreeTokensIssued);
        }
    }

    // == PersistAsync: IP-aggregate delta semantics == //

    [Fact]
    public async Task PersistAsync_PositiveDeltaOnMissingIpRow_CreatesRow()
    {
        var (db, _) = NewContext();
        using (db)
        {
            await new EfUsageStore(db).PersistAsync(Balance(), ClientIp, ipIssuedDelta: 400);

            Assert.Equal(400, (await db.IpFreeUsages.SingleAsync()).FreeTokensIssued);
        }
    }

    [Fact]
    public async Task PersistAsync_NegativeDeltaOnMissingIpRow_DoesNotCreateRow()
    {
        var (db, _) = NewContext();
        using (db)
        {
            await new EfUsageStore(db).PersistAsync(Balance(), ClientIp, ipIssuedDelta: -400);

            Assert.Empty(db.IpFreeUsages);
        }
    }

    [Fact]
    public async Task PersistAsync_RefundBelowZero_FloorsIpAggregateAtZero()
    {
        var (db, _) = NewContext();
        using (db)
        {
            db.IpFreeUsages.Add(new IpFreeUsage { Ip = ClientIp, FreeTokensIssued = 100, FirstSeenUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();

            await new EfUsageStore(db).PersistAsync(Balance(), ClientIp, ipIssuedDelta: -500);

            Assert.Equal(0, (await db.IpFreeUsages.SingleAsync()).FreeTokensIssued);
        }
    }

    // == PersistAsync: the single-unit-of-work invariant == //

    [Fact]
    public async Task PersistAsync_BalanceIpDeltaAndLedger_IsExactlyOneSaveChanges()
    {
        var (db, saves) = NewContext();
        using (db)
        {
            var entry = new UsageLedgerEntry
            {
                ObjectId  = ObjectId,
                InputTokens = 10, OutputTokens = 5,
                CostUsd = 0.02m, ProviderCostUsd = 0.01m,
                Feature = "Tutoring:Guidance", TimestampUtc = DateTime.UtcNow
            };

            await new EfUsageStore(db).PersistAsync(Balance(freeUsed: 15), ClientIp, ipIssuedDelta: 15, entry);

            Assert.Equal(1, saves.Count);   // the whole phase outcome lands in one round-trip
            Assert.Single(db.CreditBalances);
            Assert.Single(db.IpFreeUsages);
            Assert.Single(db.UsageLedgerEntries);
        }
    }
}
