// == Billing Controller Tests == //
using System.Text;
using CodeSmith.Api.Controllers;
using CodeSmith.Api.DTOs.Billing;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models.Usage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace CodeSmith.Tests.Api;

public class BillingControllerTests
{
    private readonly IBillingService _billing = Substitute.For<IBillingService>();
    private readonly BillingController _controller;

    public BillingControllerTests()
    {
        _controller = new BillingController(_billing);
    }

    // == Checkout == //

    [Fact]
    public async Task CreateCheckout_ReturnsOkWithUrl()
    {
        _billing.CreateCheckoutSessionAsync("price_ok", Arg.Any<CancellationToken>()).Returns("https://stripe/checkout");

        var result = Assert.IsType<OkObjectResult>(
            await _controller.CreateCheckout(new CheckoutRequest { PriceId = "price_ok" }, CancellationToken.None));

        var body = Assert.IsType<CheckoutResponse>(result.Value);
        Assert.Equal("https://stripe/checkout", body.Url);
    }

    // == Webhook == //

    [Fact]
    public async Task Webhook_ReadsRawBodyAndSignature_ReturnsOk()
    {
        var http = new DefaultHttpContext();
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"raw\":\"payload\"}"));
        http.Request.Headers["Stripe-Signature"] = "test-signature";
        _controller.ControllerContext = new ControllerContext { HttpContext = http };

        _billing.HandleWebhookAsync("{\"raw\":\"payload\"}", "test-signature", Arg.Any<CancellationToken>())
            .Returns(WebhookResult.Credited);

        var result = await _controller.Webhook(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        await _billing.Received(1).HandleWebhookAsync("{\"raw\":\"payload\"}", "test-signature", Arg.Any<CancellationToken>());
    }

    // == Balance == //

    [Fact]
    public async Task GetBalance_ReturnsOkWithPaidCredits()
    {
        _billing.GetBalanceAsync(Arg.Any<CancellationToken>()).Returns(42.5m);

        var result = Assert.IsType<OkObjectResult>(await _controller.GetBalance(CancellationToken.None));

        var body = Assert.IsType<BalanceResponse>(result.Value);
        Assert.Equal(42.5m, body.PaidCreditsUsd);
    }

    // == Ledger == //

    [Fact]
    public async Task GetLedger_MapsAmountFromCostUsd_AndDoesNotExposeProviderCost()
    {
        var entries = new List<UsageLedgerEntry>
        {
            new() { Type = LedgerEntryType.TopUp, CostUsd = 10m, ProviderCostUsd = null, Feature = "Billing:TopUp", TimestampUtc = DateTime.UtcNow },
            new() { Type = LedgerEntryType.Spend, CostUsd = 0.5m, ProviderCostUsd = 0.25m, Feature = "Tutoring:Guidance", TimestampUtc = DateTime.UtcNow }
        };
        _billing.GetRecentLedgerAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(entries);

        var result = Assert.IsType<OkObjectResult>(await _controller.GetLedger(20, CancellationToken.None));
        var response = Assert.IsAssignableFrom<IEnumerable<LedgerEntryResponse>>(result.Value).ToList();

        Assert.Equal(2, response.Count);
        Assert.Equal(10m, response[0].AmountUsd);          // TopUp credited amount
        Assert.Equal(0.5m, response[1].AmountUsd);         // Spend charged amount (NOT the 0.25 provider cost)
        // Structural guarantee: LedgerEntryResponse has no provider-cost property to leak.
        Assert.Null(typeof(LedgerEntryResponse).GetProperty("ProviderCostUsd"));
    }

    [Fact]
    public async Task GetLedger_ClampsTakeToUpperBound()
    {
        _billing.GetRecentLedgerAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new List<UsageLedgerEntry>());

        await _controller.GetLedger(take: 1000, CancellationToken.None);

        await _billing.Received(1).GetRecentLedgerAsync(100, Arg.Any<CancellationToken>());
    }
}
