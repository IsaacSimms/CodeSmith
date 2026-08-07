// == Stripe Billing Service Tests == //
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Infrastructure.Billing;
using CodeSmith.Infrastructure.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;
using Stripe.Checkout;

namespace CodeSmith.Tests.Infrastructure.Billing;

public class StripeBillingServiceTests
{
    private const string WebhookSecret = "whsec_test";

    private readonly IStripeEventReader _eventReader = Substitute.For<IStripeEventReader>();
    private readonly IStripeCreditStore _creditStore = Substitute.For<IStripeCreditStore>();
    private readonly ICreditBalanceRepository _balanceRepo = Substitute.For<ICreditBalanceRepository>();
    private readonly IUsageLedgerRepository _ledgerRepo = Substitute.For<IUsageLedgerRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IStripePriceReader _priceReader = Substitute.For<IStripePriceReader>();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    private StripeBillingService BuildService(params string[] priceIds)
    {
        var stripe = Options.Create(new StripeOptions
        {
            SecretKey = "sk_test",
            WebhookSecret = WebhookSecret,
            PriceIds = priceIds.Length > 0 ? priceIds : new[] { "price_ok" },
            SuccessUrl = "https://app/success",
            CancelUrl = "https://app/cancel"
        });
        // Default values only — avoids fighting FreeTokenQuota rename WIP on 001.
        var usage = Options.Create(new UsageOptions());

        return new StripeBillingService(
            stripe, usage, _eventReader, _creditStore, _balanceRepo, _ledgerRepo, _currentUser,
            _priceReader, _cache, Substitute.For<ILogger<StripeBillingService>>());
    }

    private static Price MakePrice(
        string id,
        long? unitAmount,
        string currency,
        bool active,
        string? productName)
    {
        return new Price
        {
            Id = id,
            UnitAmount = unitAmount,
            Currency = currency,
            Active = active,
            Product = productName is null
                ? null
                : new Product { Name = productName }
        };
    }

    private static Event CheckoutCompleted(string eventId, long? amountTotal, string currency, string? objectId)
    {
        var session = new Session
        {
            AmountTotal = amountTotal,
            Currency = currency,
            Metadata = objectId is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["objectId"] = objectId }
        };
        return new Event
        {
            Id = eventId,
            Type = "checkout.session.completed",
            Data = new EventData { Object = session }
        };
    }

    private void ArrangeEvent(Event stripeEvent)
        => _eventReader.Read(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(stripeEvent);

    // == Webhook: happy path == //

    [Fact]
    public async Task HandleWebhookAsync_ValidCheckout_CreditsStoreAndReturnsCredited()
    {
        ArrangeEvent(CheckoutCompleted("evt_1", amountTotal: 1000, currency: "usd", objectId: "user-1"));
        _creditStore.ApplyTopUpAsync("evt_1", "user-1", 10m, 20_000, Arg.Any<CancellationToken>())
            .Returns(TopUpOutcome.Applied);

        var result = await BuildService().HandleWebhookAsync("body", "sig");

        Assert.Equal(WebhookResult.Credited, result);
        await _creditStore.Received(1).ApplyTopUpAsync("evt_1", "user-1", 10m, 20_000, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWebhookAsync_ConvertsCentsToUsd()
    {
        ArrangeEvent(CheckoutCompleted("evt_1", amountTotal: 2500, currency: "usd", objectId: "user-1"));
        _creditStore.ApplyTopUpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(TopUpOutcome.Applied);

        await BuildService().HandleWebhookAsync("body", "sig");

        await _creditStore.Received(1).ApplyTopUpAsync("evt_1", "user-1", 25m, 20_000, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWebhookAsync_StoreReportsDuplicate_ReturnsAlreadyProcessed()
    {
        ArrangeEvent(CheckoutCompleted("evt_dup", amountTotal: 1000, currency: "usd", objectId: "user-1"));
        _creditStore.ApplyTopUpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(TopUpOutcome.AlreadyProcessed);

        var result = await BuildService().HandleWebhookAsync("body", "sig");

        Assert.Equal(WebhookResult.AlreadyProcessed, result);
    }

    // == Webhook: guards that must NOT credit == //

    [Fact]
    public async Task HandleWebhookAsync_NonUsdCurrency_IgnoredWithoutCrediting()
    {
        ArrangeEvent(CheckoutCompleted("evt_1", amountTotal: 1000, currency: "eur", objectId: "user-1"));

        var result = await BuildService().HandleWebhookAsync("body", "sig");

        Assert.Equal(WebhookResult.Ignored, result);
        await _creditStore.DidNotReceiveWithAnyArgs().ApplyTopUpAsync(default!, default!, default, default, default);
    }

    [Fact]
    public async Task HandleWebhookAsync_MissingObjectIdMetadata_IgnoredWithoutCrediting()
    {
        ArrangeEvent(CheckoutCompleted("evt_1", amountTotal: 1000, currency: "usd", objectId: null));

        var result = await BuildService().HandleWebhookAsync("body", "sig");

        Assert.Equal(WebhookResult.Ignored, result);
        await _creditStore.DidNotReceiveWithAnyArgs().ApplyTopUpAsync(default!, default!, default, default, default);
    }

    [Fact]
    public async Task HandleWebhookAsync_NonCheckoutEvent_Ignored()
    {
        var evt = new Event { Id = "evt_1", Type = "payment_intent.succeeded", Data = new EventData { Object = new PaymentIntent() } };
        ArrangeEvent(evt);

        var result = await BuildService().HandleWebhookAsync("body", "sig");

        Assert.Equal(WebhookResult.Ignored, result);
        await _creditStore.DidNotReceiveWithAnyArgs().ApplyTopUpAsync(default!, default!, default, default, default);
    }

    [Fact]
    public async Task HandleWebhookAsync_InvalidSignature_ThrowsWebhookSignatureException()
    {
        _eventReader.Read(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Throws(new StripeException("bad signature"));

        await Assert.ThrowsAsync<WebhookSignatureException>(
            () => BuildService().HandleWebhookAsync("body", "sig"));
    }

    // == Checkout validation == //

    [Fact]
    public async Task CreateCheckoutSessionAsync_UnknownPrice_ThrowsInvalidPriceException()
    {
        _currentUser.ObjectId.Returns("user-1");

        await Assert.ThrowsAsync<InvalidPriceException>(
            () => BuildService().CreateCheckoutSessionAsync("price_not_allowed"));
    }

    // == Read endpoints == //

    [Fact]
    public async Task GetBalanceAsync_NoBalanceRow_ReturnsZero()
    {
        _currentUser.ObjectId.Returns("user-1");
        _balanceRepo.GetAsync("user-1", Arg.Any<CancellationToken>()).Returns((CodeSmith.Core.Models.Usage.CreditBalance?)null);

        var balance = await BuildService().GetBalanceAsync();

        Assert.Equal(0m, balance);
    }

    // == Pack catalog == //

    [Fact]
    public async Task GetPacksAsync_ReturnsAllowListedPacksInPriceIdsOrder()
    {
        _priceReader.GetWithProductAsync("price_b", Arg.Any<CancellationToken>())
            .Returns(MakePrice("price_b", 2500, "usd", active: true, "Starter"));
        _priceReader.GetWithProductAsync("price_a", Arg.Any<CancellationToken>())
            .Returns(MakePrice("price_a", 1000, "usd", active: true, "Mini"));

        var packs = await BuildService("price_b", "price_a").GetPacksAsync();

        Assert.Equal(2, packs.Count);
        Assert.Equal("price_b", packs[0].PriceId);
        Assert.Equal("Starter", packs[0].Name);
        Assert.Equal(25m, packs[0].Amount);
        Assert.Equal("usd", packs[0].Currency);
        Assert.Equal("price_a", packs[1].PriceId);
        Assert.Equal("Mini", packs[1].Name);
        Assert.Equal(10m, packs[1].Amount);
    }

    [Fact]
    public async Task GetPacksAsync_SkipsUnusablePrices_ReturnsRemaining()
    {
        // missing
        _priceReader.GetWithProductAsync("price_missing", Arg.Any<CancellationToken>())
            .Throws(new StripeException("No such price")
            {
                HttpStatusCode = System.Net.HttpStatusCode.NotFound,
                StripeError = new StripeError { Code = "resource_missing" }
            });
        // inactive
        _priceReader.GetWithProductAsync("price_inactive", Arg.Any<CancellationToken>())
            .Returns(MakePrice("price_inactive", 1000, "usd", active: false, "Retired"));
        // non-USD
        _priceReader.GetWithProductAsync("price_eur", Arg.Any<CancellationToken>())
            .Returns(MakePrice("price_eur", 1000, "eur", active: true, "Euro Pack"));
        // blank product name
        _priceReader.GetWithProductAsync("price_blank", Arg.Any<CancellationToken>())
            .Returns(MakePrice("price_blank", 1000, "usd", active: true, "   "));
        // usable
        _priceReader.GetWithProductAsync("price_ok", Arg.Any<CancellationToken>())
            .Returns(MakePrice("price_ok", 1500, "usd", active: true, "Good Pack"));

        var packs = await BuildService(
            "price_missing", "price_inactive", "price_eur", "price_blank", "price_ok").GetPacksAsync();

        Assert.Single(packs);
        Assert.Equal("price_ok", packs[0].PriceId);
        Assert.Equal(15m, packs[0].Amount);
    }

    [Fact]
    public async Task GetPacksAsync_AllUnusable_ReturnsEmptyList()
    {
        _priceReader.GetWithProductAsync("price_dead", Arg.Any<CancellationToken>())
            .Returns(MakePrice("price_dead", 1000, "usd", active: false, "Dead"));

        var packs = await BuildService("price_dead").GetPacksAsync();

        Assert.Empty(packs);
    }

    [Fact]
    public async Task GetPacksAsync_StripeTransportFailure_ThrowsBillingServiceException()
    {
        _priceReader.GetWithProductAsync("price_ok", Arg.Any<CancellationToken>())
            .Throws(new StripeException("connection reset"));

        var ex = await Assert.ThrowsAsync<BillingServiceException>(
            () => BuildService("price_ok").GetPacksAsync());

        Assert.Contains("Stripe", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetPacksAsync_SecondCallWithinTtl_DoesNotHitStripe()
    {
        _priceReader.GetWithProductAsync("price_ok", Arg.Any<CancellationToken>())
            .Returns(MakePrice("price_ok", 1000, "usd", active: true, "Mini"));

        var service = BuildService("price_ok");
        await service.GetPacksAsync();
        await service.GetPacksAsync();

        await _priceReader.Received(1).GetWithProductAsync("price_ok", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPacksAsync_FailureIsNotCached_RetriesStripe()
    {
        _priceReader.GetWithProductAsync("price_ok", Arg.Any<CancellationToken>())
            .Throws(new StripeException("connection reset"));

        var service = BuildService("price_ok");
        await Assert.ThrowsAsync<BillingServiceException>(() => service.GetPacksAsync());
        await Assert.ThrowsAsync<BillingServiceException>(() => service.GetPacksAsync());

        await _priceReader.Received(2).GetWithProductAsync("price_ok", Arg.Any<CancellationToken>());
    }
}
