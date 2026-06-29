// == Usage Enforcer Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models.Usage;
using CodeSmith.Infrastructure.Configuration;
using CodeSmith.Infrastructure.Services.Usage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CodeSmith.Tests.Infrastructure.Usage;

public class UsageEnforcerTests
{
    private const string ObjectId = "user-1";

    // == Helpers == //

    private static UsageEnforcer BuildEnforcer(
        ICreditBalanceRepository balanceRepo,
        IUsageLedgerRepository?  ledgerRepo = null,
        ILlmPricing?             pricing    = null,
        IIpFreeUsageRepository?  ipRepo     = null,
        long                     freeQuota  = 0)
        => new(
            balanceRepo,
            ledgerRepo ?? Substitute.For<IUsageLedgerRepository>(),
            ipRepo     ?? Substitute.For<IIpFreeUsageRepository>(),
            pricing    ?? Substitute.For<ILlmPricing>(),
            new UserUsageLock(),
            Options.Create(new UsageOptions { FreeMonthlyTokenQuota = freeQuota }),
            Substitute.For<ILogger<UsageEnforcer>>());

    // == Concurrency: the regression test for the per-user lock == //

    [Fact]
    public async Task RecordActualAsync_ConcurrentCallsForSameUser_DeductEveryCallWithoutLostUpdates()
    {
        // Backing store models a real read-modify-write: GetAsync returns a snapshot, SaveAsync writes back.
        // Without per-user serialization, interleaved deductions lose updates and the final balance is too high.
        var repo = new SnapshotBalanceRepository(new CreditBalance
        {
            ObjectId               = ObjectId,
            FreeQuotaMax           = 0,        // force everything onto paid credits
            PaidCreditsBalance     = 100m,
            FirstSeenUtc           = DateTime.UtcNow
        });

        var enforcer = BuildEnforcer(repo);

        const int calls = 50;
        var tasks = Enumerable.Range(0, calls).Select(_ =>
            enforcer.RecordActualAsync(ObjectId, clientIp: null, AiProvider.Anthropic, "model", actualInput: 1, actualOutput: 0, chargeUsd: 1m, providerCostUsd: 0.5m));

        await Task.WhenAll(tasks);

        // Paid balance is debited the charge, not the raw cost
        Assert.Equal(100m - calls, repo.Current.PaidCreditsBalance);
    }

    [Fact]
    public async Task RecordActualAsync_PaidPath_LedgerRecordsChargeAndProviderCost()
    {
        var repo = new SnapshotBalanceRepository(new CreditBalance
        {
            ObjectId           = ObjectId,
            FreeQuotaMax       = 0,        // force onto paid credits
            PaidCreditsBalance = 100m,
            FirstSeenUtc       = DateTime.UtcNow
        });

        UsageLedgerEntry? captured = null;
        var ledger = Substitute.For<IUsageLedgerRepository>();
        ledger.AppendAsync(Arg.Do<UsageLedgerEntry>(e => captured = e), Arg.Any<CancellationToken>());

        var enforcer = BuildEnforcer(repo, ledgerRepo: ledger);

        await enforcer.RecordActualAsync(ObjectId, clientIp: null, AiProvider.Xai, "grok-4.3", actualInput: 1, actualOutput: 0, chargeUsd: 2m, providerCostUsd: 1m);

        Assert.NotNull(captured);
        Assert.Equal(2m, captured!.CostUsd);          // charged amount
        Assert.Equal(1m, captured.ProviderCostUsd);   // raw provider cost
        Assert.Equal(98m, repo.Current.PaidCreditsBalance);
    }

    // == Check gate == //

    [Fact]
    public async Task CheckAndReserveAsync_WithNoFreeQuotaAndNoCredits_ThrowsInsufficientQuota()
    {
        var repo = Substitute.For<ICreditBalanceRepository>();
        repo.GetAsync(ObjectId, Arg.Any<CancellationToken>()).Returns((CreditBalance?)null); // new balance with freeQuota=0

        var pricing = Substitute.For<ILlmPricing>();
        pricing.EstimateUpperBoundCost(Arg.Any<AiProvider>(), Arg.Any<int>(), Arg.Any<int>()).Returns(0.5m);

        var enforcer = BuildEnforcer(repo, pricing: pricing, freeQuota: 0);

        await Assert.ThrowsAsync<InsufficientQuotaException>(
            () => enforcer.CheckAndReserveAsync(ObjectId, clientIp: null, AiProvider.Anthropic, 100, 100));
    }

    [Fact]
    public async Task CheckAndReserveAsync_WhenFreeQuotaCovers_DoesNotThrow()
    {
        var repo = Substitute.For<ICreditBalanceRepository>();
        repo.GetAsync(ObjectId, Arg.Any<CancellationToken>()).Returns((CreditBalance?)null); // new balance gets freeQuota below

        var pricing = Substitute.For<ILlmPricing>();
        pricing.EstimateUpperBoundCost(Arg.Any<AiProvider>(), Arg.Any<int>(), Arg.Any<int>()).Returns(0.5m);

        var enforcer = BuildEnforcer(repo, pricing: pricing, freeQuota: 100_000);

        // Should not throw — free quota of 20k covers a ~20-token estimate (within window)
        await enforcer.CheckAndReserveAsync(ObjectId, clientIp: null, AiProvider.Anthropic, 10, 10);
    }

    // == Fake repository that models DB read/write hazard window == //

    private sealed class SnapshotBalanceRepository : ICreditBalanceRepository
    {
        private CreditBalance _stored;

        public SnapshotBalanceRepository(CreditBalance seed) => _stored = Clone(seed);

        public CreditBalance Current => _stored;

        public async Task<CreditBalance?> GetAsync(string objectId, CancellationToken ct = default)
        {
            await Task.Yield();            // widen the read→modify→write window
            return Clone(_stored);          // snapshot, as a real DB read would return
        }

        public async Task SaveAsync(CreditBalance balance, CancellationToken ct = default)
        {
            await Task.Yield();
            _stored = Clone(balance);
        }

        private static CreditBalance Clone(CreditBalance b) => new()
        {
            ObjectId                = b.ObjectId,
            PaidCreditsBalance      = b.PaidCreditsBalance,
            FreeTokensUsedInWindow  = b.FreeTokensUsedInWindow,
            FreeQuotaMax            = b.FreeQuotaMax,
            FirstSeenUtc            = b.FirstSeenUtc
        };
    }
}
