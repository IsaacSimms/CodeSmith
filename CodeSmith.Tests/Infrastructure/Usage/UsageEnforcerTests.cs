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
    private const string ClientIp = "1.2.3.4";

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

    // == Headline: the reservation actually holds, so a fan-out can't all pass one gate == //

    [Fact]
    public async Task ReserveAsync_ConcurrentFanOutForSameUser_AdmitsOnlyWhatTheBalanceCovers()
    {
        // Paid balance covers exactly ONE call's upper-bound hold; free quota disabled. Fails if
        // ReserveAsync does not persist the hold before releasing the per-user lock (the old check-only
        // behaviour), because every concurrent reservation would then see the full balance and pass.
        var repo = new SnapshotBalanceRepository(new CreditBalance
        {
            ObjectId           = ObjectId,
            FreeQuotaMax       = 0,
            PaidCreditsBalance = 1m,
            FirstSeenUtc       = DateTime.UtcNow
        });

        var pricing = Substitute.For<ILlmPricing>();
        pricing.EstimateUpperBoundCost(Arg.Any<AiProvider>(), Arg.Any<int>(), Arg.Any<int>()).Returns(1m);

        var enforcer = BuildEnforcer(repo, pricing: pricing);

        const int calls = 50;
        var successes = 0;
        var failures  = 0;

        var tasks = Enumerable.Range(0, calls).Select(async _ =>
        {
            try
            {
                await enforcer.ReserveAsync(ObjectId, ClientIp, AiProvider.Anthropic, 100, 100);
                Interlocked.Increment(ref successes);
            }
            catch (InsufficientQuotaException)
            {
                Interlocked.Increment(ref failures);
            }
        });

        await Task.WhenAll(tasks);

        Assert.Equal(1, successes);              // only the single call the balance could cover
        Assert.Equal(calls - 1, failures);
        Assert.Equal(0m, repo.Current.PaidCreditsBalance); // never oversold / negative
    }

    // == Reserve persists, so a second reserve after free is consumed is rejected == //

    [Fact]
    public async Task ReserveAsync_AfterPriorReserveConsumesFreeQuota_ThrowsInsufficientQuota()
    {
        var repo = new SnapshotBalanceRepository(ActiveBalance(freeQuotaMax: 20_000, freeTokensUsed: 19_900));

        var ipRepo = Substitute.For<IIpFreeUsageRepository>();
        ipRepo.GetIssuedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(0L);

        var enforcer = BuildEnforcer(repo, pricing: CreatePerTokenPricing(), ipRepo: ipRepo);

        // First reserve holds the last 100 free tokens (50 + 50).
        await enforcer.ReserveAsync(ObjectId, ClientIp, AiProvider.Anthropic, 50, 50);
        Assert.Equal(20_000, repo.Current.FreeTokensUsedInWindow);

        // Nothing left and no paid credits — the next reserve is rejected.
        await Assert.ThrowsAsync<InsufficientQuotaException>(
            () => enforcer.ReserveAsync(ObjectId, ClientIp, AiProvider.Anthropic, 10, 10));
    }

    // == Settle reconciles the hold to actuals == //

    [Fact]
    public async Task SettleAsync_AfterPaidReserve_RefundsUpperBoundAndChargesActual()
    {
        var repo = new SnapshotBalanceRepository(new CreditBalance
        {
            ObjectId           = ObjectId,
            FreeQuotaMax       = 0,            // force onto paid credits
            PaidCreditsBalance = 100m,
            FirstSeenUtc       = DateTime.UtcNow
        });

        var pricing = Substitute.For<ILlmPricing>();
        pricing.EstimateUpperBoundCost(Arg.Any<AiProvider>(), Arg.Any<int>(), Arg.Any<int>()).Returns(10m); // hold $10

        UsageLedgerEntry? captured = null;
        var ledger = Substitute.For<IUsageLedgerRepository>();
        _ = ledger.AppendAsync(Arg.Do<UsageLedgerEntry>(e => captured = e), Arg.Any<CancellationToken>());

        var enforcer = BuildEnforcer(repo, ledgerRepo: ledger, pricing: pricing);

        var reservation = await enforcer.ReserveAsync(ObjectId, ClientIp, AiProvider.Xai, 100, 100);
        Assert.Equal(90m, repo.Current.PaidCreditsBalance); // $10 held up front

        await enforcer.SettleAsync(reservation, "grok-4.3", actualInput: 1, actualOutput: 0, chargeUsd: 2m, providerCostUsd: 1m);

        Assert.Equal(98m, repo.Current.PaidCreditsBalance); // refund $10 hold, charge actual $2
        Assert.NotNull(captured);
        Assert.Equal(2m, captured!.CostUsd);
        Assert.Equal(1m, captured.ProviderCostUsd);
    }

    // == Release refunds the hold and writes no ledger == //

    [Fact]
    public async Task ReleaseAsync_AfterFreeReserve_RestoresBalanceAndWritesNoLedger()
    {
        var repo = new SnapshotBalanceRepository(ActiveBalance(freeQuotaMax: 20_000, freeTokensUsed: 0, paid: 50m));

        var ipRepo = Substitute.For<IIpFreeUsageRepository>();
        ipRepo.GetIssuedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(0L);

        var ledger = Substitute.For<IUsageLedgerRepository>();
        var enforcer = BuildEnforcer(repo, ledgerRepo: ledger, pricing: CreatePerTokenPricing(), ipRepo: ipRepo);

        var reservation = await enforcer.ReserveAsync(ObjectId, ClientIp, AiProvider.Anthropic, 100, 100);
        Assert.Equal(200, repo.Current.FreeTokensUsedInWindow); // 200 free tokens held

        await enforcer.ReleaseAsync(reservation);

        Assert.Equal(0, repo.Current.FreeTokensUsedInWindow);   // hold fully restored
        Assert.Equal(50m, repo.Current.PaidCreditsBalance);
        await ledger.DidNotReceive().AppendAsync(Arg.Any<UsageLedgerEntry>(), Arg.Any<CancellationToken>());
        await ipRepo.Received().AddIssuedAsync(ClientIp, 200, Arg.Any<CancellationToken>());   // reserve grant
        await ipRepo.Received().AddIssuedAsync(ClientIp, -200, Arg.Any<CancellationToken>());  // release refund
    }

    // == Settle with an empty hold behaves like a pure record (lock + split logic) == //

    [Fact]
    public async Task SettleAsync_ConcurrentCallsForSameUser_DeductEveryCallWithoutLostUpdates()
    {
        var repo = new SnapshotBalanceRepository(new CreditBalance
        {
            ObjectId           = ObjectId,
            FreeQuotaMax       = 0,            // everything on paid credits
            PaidCreditsBalance = 100m,
            FirstSeenUtc       = DateTime.UtcNow
        });

        var enforcer = BuildEnforcer(repo);

        const int calls = 50;
        var tasks = Enumerable.Range(0, calls).Select(_ =>
            enforcer.SettleAsync(EmptyReservation(), "model", actualInput: 1, actualOutput: 0, chargeUsd: 1m, providerCostUsd: 0.5m));

        await Task.WhenAll(tasks);

        Assert.Equal(100m - calls, repo.Current.PaidCreditsBalance); // every deduction lands, no lost updates
    }

    [Fact]
    public async Task SettleAsync_PartialFree_SplitsFreeAndPaidDeduction()
    {
        var repo = new SnapshotBalanceRepository(ActiveBalance(freeQuotaMax: 20_000, freeTokensUsed: 19_500, paid: 15m));

        var ipRepo = Substitute.For<IIpFreeUsageRepository>();
        ipRepo.GetIssuedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(0L);

        var enforcer = BuildEnforcer(repo, pricing: CreatePerTokenPricing(), ipRepo: ipRepo);

        await enforcer.SettleAsync(EmptyReservation(), "model", actualInput: 100, actualOutput: 1700, chargeUsd: 18m, providerCostUsd: 9m);

        Assert.Equal(20_000, repo.Current.FreeTokensUsedInWindow);
        Assert.Equal(2m, repo.Current.PaidCreditsBalance); // 1300/1800 of $18 charge = $13; 15 - 13 = 2
        await ipRepo.Received(1).AddIssuedAsync(ClientIp, 500, Arg.Any<CancellationToken>());
    }

    // == Reserve gate: coverage boundaries == //

    [Fact]
    public async Task ReserveAsync_WithNoFreeQuotaAndNoCredits_ThrowsInsufficientQuota()
    {
        var repo = Substitute.For<ICreditBalanceRepository>();
        repo.GetOrCreateAsync(ObjectId, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(ci => CreditBalance.CreateNew(ObjectId, ci.ArgAt<long>(1))); // new balance with freeQuota=0

        var pricing = Substitute.For<ILlmPricing>();
        pricing.EstimateUpperBoundCost(Arg.Any<AiProvider>(), Arg.Any<int>(), Arg.Any<int>()).Returns(0.5m);

        var enforcer = BuildEnforcer(repo, pricing: pricing, freeQuota: 0);

        await Assert.ThrowsAsync<InsufficientQuotaException>(
            () => enforcer.ReserveAsync(ObjectId, ClientIp, AiProvider.Anthropic, 100, 100));
    }

    [Fact]
    public async Task ReserveAsync_WhenFreeQuotaCovers_DoesNotThrow()
    {
        var repo = Substitute.For<ICreditBalanceRepository>();
        repo.GetOrCreateAsync(ObjectId, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(ci => CreditBalance.CreateNew(ObjectId, ci.ArgAt<long>(1))); // new balance gets freeQuota below

        var pricing = Substitute.For<ILlmPricing>();
        pricing.EstimateUpperBoundCost(Arg.Any<AiProvider>(), Arg.Any<int>(), Arg.Any<int>()).Returns(0.5m);

        var enforcer = BuildEnforcer(repo, pricing: pricing, freeQuota: 100_000);

        var reservation = await enforcer.ReserveAsync(ObjectId, ClientIp, AiProvider.Anthropic, 10, 10);

        Assert.True(reservation.UsedFree); // ~20-token estimate covered by the free window
    }

    [Fact]
    public async Task ReserveAsync_ExhaustedObjectQuota_ThrowsInsufficientQuota()
    {
        var repo = Substitute.For<ICreditBalanceRepository>();
        repo.GetOrCreateAsync(ObjectId, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(ActiveBalance(freeQuotaMax: 20_000, freeTokensUsed: 20_000));

        var ipRepo = Substitute.For<IIpFreeUsageRepository>();
        ipRepo.GetIssuedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(0L);

        var pricing = Substitute.For<ILlmPricing>();
        pricing.EstimateUpperBoundCost(Arg.Any<AiProvider>(), Arg.Any<int>(), Arg.Any<int>()).Returns(1m);

        var enforcer = BuildEnforcer(repo, pricing: pricing, ipRepo: ipRepo);

        await Assert.ThrowsAsync<InsufficientQuotaException>(
            () => enforcer.ReserveAsync(ObjectId, ClientIp, AiProvider.Anthropic, 100, 100));
    }

    [Fact]
    public async Task ReserveAsync_ExhaustedIpQuota_ThrowsInsufficientQuota()
    {
        var repo = Substitute.For<ICreditBalanceRepository>();
        repo.GetOrCreateAsync(ObjectId, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(ActiveBalance(freeQuotaMax: 20_000, freeTokensUsed: 0));

        var ipRepo = Substitute.For<IIpFreeUsageRepository>();
        ipRepo.GetIssuedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(60_000L);

        var pricing = Substitute.For<ILlmPricing>();
        pricing.EstimateUpperBoundCost(Arg.Any<AiProvider>(), Arg.Any<int>(), Arg.Any<int>()).Returns(1m);

        var enforcer = BuildEnforcer(repo, pricing: pricing, ipRepo: ipRepo);

        await Assert.ThrowsAsync<InsufficientQuotaException>(
            () => enforcer.ReserveAsync(ObjectId, ClientIp, AiProvider.Anthropic, 100, 100));
    }

    [Fact]
    public async Task ReserveAsync_ObjectExhaustedButIpHasRoom_ThrowsInsufficientQuota()
    {
        var repo = Substitute.For<ICreditBalanceRepository>();
        repo.GetOrCreateAsync(ObjectId, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(ActiveBalance(freeQuotaMax: 20_000, freeTokensUsed: 20_000));

        var ipRepo = Substitute.For<IIpFreeUsageRepository>();
        ipRepo.GetIssuedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(0L);

        var pricing = Substitute.For<ILlmPricing>();
        pricing.EstimateUpperBoundCost(Arg.Any<AiProvider>(), Arg.Any<int>(), Arg.Any<int>()).Returns(1m);

        var enforcer = BuildEnforcer(repo, pricing: pricing, ipRepo: ipRepo);

        await Assert.ThrowsAsync<InsufficientQuotaException>(
            () => enforcer.ReserveAsync(ObjectId, ClientIp, AiProvider.Anthropic, 10, 10));
    }

    [Fact]
    public async Task ReserveAsync_PartialFreeHeadroomWithPaidOverflow_DoesNotThrow()
    {
        var repo = Substitute.For<ICreditBalanceRepository>();
        repo.GetOrCreateAsync(ObjectId, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(ActiveBalance(freeQuotaMax: 20_000, freeTokensUsed: 19_500, paid: 25m));

        var ipRepo = Substitute.For<IIpFreeUsageRepository>();
        ipRepo.GetIssuedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(0L);

        var enforcer = BuildEnforcer(repo, pricing: CreatePerTokenPricing(), ipRepo: ipRepo);

        var reservation = await enforcer.ReserveAsync(ObjectId, ClientIp, AiProvider.Anthropic, 100, 2500);

        Assert.True(reservation.ReservedFreeTokens > 0);
        Assert.True(reservation.ReservedPaidUsd > 0);
    }

    [Fact]
    public async Task ReserveAsync_PartialFreeNoPaidForOverflow_ThrowsInsufficientQuota()
    {
        var repo = Substitute.For<ICreditBalanceRepository>();
        repo.GetOrCreateAsync(ObjectId, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(ActiveBalance(freeQuotaMax: 20_000, freeTokensUsed: 19_500));

        var ipRepo = Substitute.For<IIpFreeUsageRepository>();
        ipRepo.GetIssuedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(0L);

        var enforcer = BuildEnforcer(repo, pricing: CreatePerTokenPricing(), ipRepo: ipRepo);

        await Assert.ThrowsAsync<InsufficientQuotaException>(
            () => enforcer.ReserveAsync(ObjectId, ClientIp, AiProvider.Anthropic, 100, 2500));
    }

    [Fact]
    public async Task ReserveAsync_WindowExpired_ThrowsInsufficientQuota()
    {
        var repo = Substitute.For<ICreditBalanceRepository>();
        repo.GetOrCreateAsync(ObjectId, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(ActiveBalance(freeQuotaMax: 20_000, freeTokensUsed: 0, firstSeen: DateTime.UtcNow.AddHours(-49)));

        var ipRepo = Substitute.For<IIpFreeUsageRepository>();
        ipRepo.GetIssuedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(0L);

        var pricing = Substitute.For<ILlmPricing>();
        pricing.EstimateUpperBoundCost(Arg.Any<AiProvider>(), Arg.Any<int>(), Arg.Any<int>()).Returns(1m);

        var enforcer = BuildEnforcer(repo, pricing: pricing, ipRepo: ipRepo);

        await Assert.ThrowsAsync<InsufficientQuotaException>(
            () => enforcer.ReserveAsync(ObjectId, ClientIp, AiProvider.Anthropic, 10, 10));
    }

    // == Fixtures == //

    private static CreditBalance ActiveBalance(
        long freeQuotaMax,
        long freeTokensUsed,
        decimal paid = 0m,
        DateTime? firstSeen = null)
        => new()
        {
            ObjectId               = ObjectId,
            FreeQuotaMax           = freeQuotaMax,
            FreeTokensUsedInWindow = freeTokensUsed,
            PaidCreditsBalance     = paid,
            FirstSeenUtc           = firstSeen ?? DateTime.UtcNow
        };

    // A settled call that was never reserved (zero hold) — Settle then behaves like a pure record.
    private static UsageReservation EmptyReservation(string ip = ClientIp, AiProvider provider = AiProvider.Anthropic)
        => new()
        {
            ObjectId           = ObjectId,
            ClientIp           = ip,
            Provider           = provider,
            ReservedFreeTokens = 0,
            ReservedPaidUsd    = 0m
        };

    private static ILlmPricing CreatePerTokenPricing()
    {
        var pricing = Substitute.For<ILlmPricing>();
        pricing.EstimateUpperBoundCost(Arg.Any<AiProvider>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(call => (call.ArgAt<int>(1) + call.ArgAt<int>(2)) * 0.01m);
        pricing.ComputeChargeUsd(Arg.Any<AiProvider>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(call => (call.ArgAt<int>(2) + call.ArgAt<int>(3)) * 0.01m);
        pricing.ComputeCostUsd(Arg.Any<AiProvider>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(call => (call.ArgAt<int>(2) + call.ArgAt<int>(3)) * 0.005m);
        return pricing;
    }

    // == Fake repository that models the DB read/write hazard window == //

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

        public async Task<CreditBalance> GetOrCreateAsync(string objectId, long freeQuotaMax, CancellationToken ct = default)
        {
            await Task.Yield();
            return Clone(_stored);          // seed always present in these tests
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
