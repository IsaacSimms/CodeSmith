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
        IUsageStore  store,
        ILlmPricing? pricing   = null,
        long         freeQuota = 0)
        => new(
            store,
            pricing ?? Substitute.For<ILlmPricing>(),
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
        var store = new FakeUsageStore(new CreditBalance
        {
            ObjectId           = ObjectId,
            FreeQuotaMax       = 0,
            PaidCreditsBalance = 1m,
            FirstSeenUtc       = DateTime.UtcNow
        });

        var pricing = Substitute.For<ILlmPricing>();
        pricing.EstimateUpperBoundCost(Arg.Any<AiProvider>(), Arg.Any<int>(), Arg.Any<int>()).Returns(1m);

        var enforcer = BuildEnforcer(store, pricing);

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
        Assert.Equal(0m, store.Current.PaidCreditsBalance); // never oversold / negative
    }

    // == Reserve persists, so a second reserve after free is consumed is rejected == //

    [Fact]
    public async Task ReserveAsync_AfterPriorReserveConsumesFreeQuota_ThrowsInsufficientQuota()
    {
        var store = new FakeUsageStore(ActiveBalance(freeQuotaMax: 20_000, freeTokensUsed: 19_900));

        var enforcer = BuildEnforcer(store, CreatePerTokenPricing());

        // First reserve holds the last 100 free tokens (50 + 50).
        await enforcer.ReserveAsync(ObjectId, ClientIp, AiProvider.Anthropic, 50, 50);
        Assert.Equal(20_000, store.Current.FreeTokensUsedInWindow);

        // Nothing left and no paid credits — the next reserve is rejected.
        await Assert.ThrowsAsync<InsufficientQuotaException>(
            () => enforcer.ReserveAsync(ObjectId, ClientIp, AiProvider.Anthropic, 10, 10));
    }

    // == Settle reconciles the hold to actuals == //

    [Fact]
    public async Task SettleAsync_AfterPaidReserve_RefundsUpperBoundAndChargesActual()
    {
        var store = new FakeUsageStore(new CreditBalance
        {
            ObjectId           = ObjectId,
            FreeQuotaMax       = 0,            // force onto paid credits
            PaidCreditsBalance = 100m,
            FirstSeenUtc       = DateTime.UtcNow
        });

        var pricing = Substitute.For<ILlmPricing>();
        pricing.EstimateUpperBoundCost(Arg.Any<AiProvider>(), Arg.Any<int>(), Arg.Any<int>()).Returns(10m); // hold $10

        var enforcer = BuildEnforcer(store, pricing);

        var reservation = await enforcer.ReserveAsync(ObjectId, ClientIp, AiProvider.Xai, 100, 100);
        Assert.Equal(90m, store.Current.PaidCreditsBalance); // $10 held up front

        await enforcer.SettleAsync(reservation, "grok-4.5", actualInput: 1, actualOutput: 0, chargeUsd: 2m, providerCostUsd: 1m);

        Assert.Equal(98m, store.Current.PaidCreditsBalance); // refund $10 hold, charge actual $2
        var captured = Assert.Single(store.Ledger);
        Assert.Equal(2m, captured.CostUsd);
        Assert.Equal(1m, captured.ProviderCostUsd);
    }

    // == Release refunds the hold and writes no ledger == //

    [Fact]
    public async Task ReleaseAsync_AfterFreeReserve_RestoresBalanceAndWritesNoLedger()
    {
        var store = new FakeUsageStore(ActiveBalance(freeQuotaMax: 20_000, freeTokensUsed: 0, paid: 50m));

        var enforcer = BuildEnforcer(store, CreatePerTokenPricing());

        var reservation = await enforcer.ReserveAsync(ObjectId, ClientIp, AiProvider.Anthropic, 100, 100);
        Assert.Equal(200, store.Current.FreeTokensUsedInWindow); // 200 free tokens held
        Assert.Equal(200, store.IpIssued);                       // reserve grant reached the IP aggregate

        await enforcer.ReleaseAsync(reservation);

        Assert.Equal(0, store.Current.FreeTokensUsedInWindow);   // hold fully restored
        Assert.Equal(50m, store.Current.PaidCreditsBalance);
        Assert.Equal(0, store.IpIssued);                         // release refunded the IP grant
        Assert.Empty(store.Ledger);
    }

    // == Settle with an empty hold behaves like a pure record (lock + split logic) == //

    [Fact]
    public async Task SettleAsync_ConcurrentCallsForSameUser_DeductEveryCallWithoutLostUpdates()
    {
        var store = new FakeUsageStore(new CreditBalance
        {
            ObjectId           = ObjectId,
            FreeQuotaMax       = 0,            // everything on paid credits
            PaidCreditsBalance = 100m,
            FirstSeenUtc       = DateTime.UtcNow
        });

        var enforcer = BuildEnforcer(store);

        const int calls = 50;
        var tasks = Enumerable.Range(0, calls).Select(_ =>
            enforcer.SettleAsync(EmptyReservation(), "model", actualInput: 1, actualOutput: 0, chargeUsd: 1m, providerCostUsd: 0.5m));

        await Task.WhenAll(tasks);

        Assert.Equal(100m - calls, store.Current.PaidCreditsBalance); // every deduction lands, no lost updates
    }

    [Fact]
    public async Task SettleAsync_PartialFree_SplitsFreeAndPaidDeduction()
    {
        var store = new FakeUsageStore(ActiveBalance(freeQuotaMax: 20_000, freeTokensUsed: 19_500, paid: 15m));

        var enforcer = BuildEnforcer(store, CreatePerTokenPricing());

        await enforcer.SettleAsync(EmptyReservation(), "model", actualInput: 100, actualOutput: 1700, chargeUsd: 18m, providerCostUsd: 9m);

        Assert.Equal(20_000, store.Current.FreeTokensUsedInWindow);
        Assert.Equal(2m, store.Current.PaidCreditsBalance); // 1300/1800 of $18 charge = $13; 15 - 13 = 2
        Assert.Equal(500, store.IpIssued);                  // the free portion reached the IP aggregate
    }

    // == Reserve gate: coverage boundaries == //

    [Fact]
    public async Task ReserveAsync_WithNoFreeQuotaAndNoCredits_ThrowsInsufficientQuota()
    {
        var store = new FakeUsageStore(seed: null); // unseen user → enforcer creates the canonical new balance

        var pricing = Substitute.For<ILlmPricing>();
        pricing.EstimateUpperBoundCost(Arg.Any<AiProvider>(), Arg.Any<int>(), Arg.Any<int>()).Returns(0.5m);

        var enforcer = BuildEnforcer(store, pricing, freeQuota: 0);

        await Assert.ThrowsAsync<InsufficientQuotaException>(
            () => enforcer.ReserveAsync(ObjectId, ClientIp, AiProvider.Anthropic, 100, 100));
    }

    [Fact]
    public async Task ReserveAsync_WhenFreeQuotaCovers_DoesNotThrow()
    {
        var store = new FakeUsageStore(seed: null); // unseen user seeded from UsageOptions quota below

        var pricing = Substitute.For<ILlmPricing>();
        pricing.EstimateUpperBoundCost(Arg.Any<AiProvider>(), Arg.Any<int>(), Arg.Any<int>()).Returns(0.5m);

        var enforcer = BuildEnforcer(store, pricing, freeQuota: 100_000);

        var reservation = await enforcer.ReserveAsync(ObjectId, ClientIp, AiProvider.Anthropic, 10, 10);

        Assert.True(reservation.UsedFree); // ~20-token estimate covered by the free window
    }

    [Fact]
    public async Task ReserveAsync_ExhaustedObjectQuota_ThrowsInsufficientQuota()
    {
        var store = new FakeUsageStore(ActiveBalance(freeQuotaMax: 20_000, freeTokensUsed: 20_000));

        var pricing = Substitute.For<ILlmPricing>();
        pricing.EstimateUpperBoundCost(Arg.Any<AiProvider>(), Arg.Any<int>(), Arg.Any<int>()).Returns(1m);

        var enforcer = BuildEnforcer(store, pricing);

        await Assert.ThrowsAsync<InsufficientQuotaException>(
            () => enforcer.ReserveAsync(ObjectId, ClientIp, AiProvider.Anthropic, 100, 100));
    }

    [Fact]
    public async Task ReserveAsync_ExhaustedIpQuota_ThrowsInsufficientQuota()
    {
        var store = new FakeUsageStore(ActiveBalance(freeQuotaMax: 20_000, freeTokensUsed: 0), ipIssued: 60_000);

        var pricing = Substitute.For<ILlmPricing>();
        pricing.EstimateUpperBoundCost(Arg.Any<AiProvider>(), Arg.Any<int>(), Arg.Any<int>()).Returns(1m);

        var enforcer = BuildEnforcer(store, pricing);

        await Assert.ThrowsAsync<InsufficientQuotaException>(
            () => enforcer.ReserveAsync(ObjectId, ClientIp, AiProvider.Anthropic, 100, 100));
    }

    [Fact]
    public async Task ReserveAsync_ObjectExhaustedButIpHasRoom_ThrowsInsufficientQuota()
    {
        var store = new FakeUsageStore(ActiveBalance(freeQuotaMax: 20_000, freeTokensUsed: 20_000));

        var pricing = Substitute.For<ILlmPricing>();
        pricing.EstimateUpperBoundCost(Arg.Any<AiProvider>(), Arg.Any<int>(), Arg.Any<int>()).Returns(1m);

        var enforcer = BuildEnforcer(store, pricing);

        await Assert.ThrowsAsync<InsufficientQuotaException>(
            () => enforcer.ReserveAsync(ObjectId, ClientIp, AiProvider.Anthropic, 10, 10));
    }

    [Fact]
    public async Task ReserveAsync_PartialFreeHeadroomWithPaidOverflow_DoesNotThrow()
    {
        var store = new FakeUsageStore(ActiveBalance(freeQuotaMax: 20_000, freeTokensUsed: 19_500, paid: 25m));

        var enforcer = BuildEnforcer(store, CreatePerTokenPricing());

        var reservation = await enforcer.ReserveAsync(ObjectId, ClientIp, AiProvider.Anthropic, 100, 2500);

        Assert.True(reservation.ReservedFreeTokens > 0);
        Assert.True(reservation.ReservedPaidUsd > 0);
    }

    [Fact]
    public async Task ReserveAsync_PartialFreeNoPaidForOverflow_ThrowsInsufficientQuota()
    {
        var store = new FakeUsageStore(ActiveBalance(freeQuotaMax: 20_000, freeTokensUsed: 19_500));

        var enforcer = BuildEnforcer(store, CreatePerTokenPricing());

        await Assert.ThrowsAsync<InsufficientQuotaException>(
            () => enforcer.ReserveAsync(ObjectId, ClientIp, AiProvider.Anthropic, 100, 2500));
    }

    [Fact]
    public async Task ReserveAsync_WindowExpired_ThrowsInsufficientQuota()
    {
        var store = new FakeUsageStore(ActiveBalance(freeQuotaMax: 20_000, freeTokensUsed: 0, firstSeen: DateTime.UtcNow.AddHours(-49)));

        var pricing = Substitute.For<ILlmPricing>();
        pricing.EstimateUpperBoundCost(Arg.Any<AiProvider>(), Arg.Any<int>(), Arg.Any<int>()).Returns(1m);

        var enforcer = BuildEnforcer(store, pricing);

        await Assert.ThrowsAsync<InsufficientQuotaException>(
            () => enforcer.ReserveAsync(ObjectId, ClientIp, AiProvider.Anthropic, 10, 10));
    }

    // == Release must not mint a balance row for an unseen user == //

    [Fact]
    public async Task ReleaseAsync_NoBalanceRow_RefundsIpOnlyAndCreatesNoBalance()
    {
        var store = new FakeUsageStore(seed: null, ipIssued: 500);

        var enforcer = BuildEnforcer(store);

        await enforcer.ReleaseAsync(new UsageReservation
        {
            ObjectId           = ObjectId,
            ClientIp           = ClientIp,
            Provider           = AiProvider.Anthropic,
            ReservedFreeTokens = 200,
            ReservedPaidUsd    = 5m
        });

        Assert.False(store.HasBalance);    // reversing a paid hold on a missing row would mint credits
        Assert.Equal(300, store.IpIssued); // the free-token grant is still refunded
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

    // == Fake store that models the DB read/write hazard window == //

    private sealed class FakeUsageStore : IUsageStore
    {
        private CreditBalance? _balance;
        private long _ipIssued;

        public FakeUsageStore(CreditBalance? seed, long ipIssued = 0)
        {
            _balance  = seed is null ? null : Clone(seed);
            _ipIssued = ipIssued;
        }

        public CreditBalance Current => _balance!;              // stored state, for assertions
        public bool HasBalance => _balance is not null;
        public long IpIssued => _ipIssued;
        public List<UsageLedgerEntry> Ledger { get; } = [];

        public async Task<UsageSnapshot> GetSnapshotAsync(string objectId, string clientIp, CancellationToken ct = default)
        {
            await Task.Yield();                                  // widen the read→modify→write window
            return new UsageSnapshot(_balance is null ? null : Clone(_balance), _ipIssued);
        }

        public async Task PersistAsync(CreditBalance? balance, string clientIp, long ipIssuedDelta, UsageLedgerEntry? ledgerEntry = null, CancellationToken ct = default)
        {
            await Task.Yield();
            if (balance is not null)
                _balance = Clone(balance);
            if (ipIssuedDelta != 0)
                _ipIssued = Math.Max(0, _ipIssued + ipIssuedDelta);
            if (ledgerEntry is not null)
                Ledger.Add(ledgerEntry);
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
