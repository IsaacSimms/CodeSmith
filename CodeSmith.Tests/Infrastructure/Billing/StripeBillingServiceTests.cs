// == Stripe Billing Service Tests == //
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Infrastructure.Billing;
using CodeSmith.Infrastructure.Configuration;
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

    private StripeBillingService BuildService()
    {
        var stripe = Options.Create(new StripeOptions
        {
            SecretKey = "sk_test",
            WebhookSecret = WebhookSecret,
            PriceIds = new[] { "price_ok" },
            SuccessUrl = "https://app/success",
            CancelUrl = "https://app/cancel"
        });
        var usage = Options.Create(new UsageOptions { FreeMonthlyTokenQuota = 20_000 });

        return new StripeBillingService(
            stripe, usage, _eventReader, _creditStore, _balanceRepo, _ledgerRepo, _currentUser,
            Substitute.For<ILogger<StripeBillingService>>());
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
}
