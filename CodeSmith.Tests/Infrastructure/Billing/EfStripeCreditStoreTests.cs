// == EF Stripe Credit Store Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models.Usage;
using CodeSmith.Infrastructure.Persistence;
using CodeSmith.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CodeSmith.Tests.Infrastructure.Billing;

public class EfStripeCreditStoreTests
{
    private static CodeSmithDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CodeSmithDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new CodeSmithDbContext(options);
    }

    [Fact]
    public async Task ApplyTopUpAsync_FirstTime_CreatesBalanceCreditsAndReturnsApplied()
    {
        using var db = NewContext();
        var store = new EfStripeCreditStore(db);

        var outcome = await store.ApplyTopUpAsync("evt_1", "user-1", 10m, freeQuotaMax: 20_000);

        Assert.Equal(TopUpOutcome.Applied, outcome);
        var balance = await db.CreditBalances.SingleAsync();
        Assert.Equal("user-1", balance.ObjectId);
        Assert.Equal(10m, balance.PaidCreditsBalance);
        Assert.Equal(20_000, balance.FreeQuotaMax);
    }

    [Fact]
    public async Task ApplyTopUpAsync_ExistingBalance_AddsToExistingCredits()
    {
        using var db = NewContext();
        db.CreditBalances.Add(new CreditBalance { ObjectId = "user-1", PaidCreditsBalance = 5m, FreeQuotaMax = 20_000 });
        await db.SaveChangesAsync();
        var store = new EfStripeCreditStore(db);

        await store.ApplyTopUpAsync("evt_1", "user-1", 10m, freeQuotaMax: 20_000);

        var balance = await db.CreditBalances.SingleAsync();
        Assert.Equal(15m, balance.PaidCreditsBalance);
    }

    [Fact]
    public async Task ApplyTopUpAsync_DuplicateEvent_CreditsOnlyOnce()
    {
        using var db = NewContext();
        var store = new EfStripeCreditStore(db);

        var first = await store.ApplyTopUpAsync("evt_dup", "user-1", 10m, freeQuotaMax: 20_000);
        var second = await store.ApplyTopUpAsync("evt_dup", "user-1", 10m, freeQuotaMax: 20_000);

        Assert.Equal(TopUpOutcome.Applied, first);
        Assert.Equal(TopUpOutcome.AlreadyProcessed, second);
        var balance = await db.CreditBalances.SingleAsync();
        Assert.Equal(10m, balance.PaidCreditsBalance);                    // credited once, not twice
        Assert.Equal(1, await db.UsageLedgerEntries.CountAsync());        // one ledger row, not two
    }

    [Fact]
    public async Task ApplyTopUpAsync_WritesTopUpLedgerRow_WithNullProviderAndModel()
    {
        using var db = NewContext();
        var store = new EfStripeCreditStore(db);

        await store.ApplyTopUpAsync("evt_1", "user-1", 25m, freeQuotaMax: 20_000);

        var entry = await db.UsageLedgerEntries.SingleAsync();
        Assert.Equal(LedgerEntryType.TopUp, entry.Type);
        Assert.Equal("user-1", entry.ObjectId);
        Assert.Equal(25m, entry.CostUsd);
        Assert.Null(entry.Provider);
        Assert.Null(entry.Model);
        Assert.Null(entry.ProviderCostUsd);
    }
}
