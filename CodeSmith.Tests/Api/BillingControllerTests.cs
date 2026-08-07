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
            new() { Type = LedgerEntryType.Spend, CostUsd = 0.5m, ProviderCostUsd = 0.25m, FreeTokensCovered = 0, Feature = "Tutoring:Guidance", TimestampUtc = DateTime.UtcNow }
        };
        _billing.GetRecentLedgerAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(entries);

        var result = Assert.IsType<OkObjectResult>(await _controller.GetLedger(20, CancellationToken.None));
        var response = Assert.IsAssignableFrom<IEnumerable<LedgerEntryResponse>>(result.Value).ToList();

        Assert.Equal(2, response.Count);
        Assert.Equal(10m, response[0].AmountUsd);          // TopUp credited amount
        Assert.Equal(0.5m, response[1].AmountUsd);         // Spend charged amount (NOT the 0.25 provider cost)
        // Structural guarantee: LedgerEntryResponse has no provider-cost property to leak.
        Assert.Null(typeof(LedgerEntryResponse).GetProperty("ProviderCostUsd"));
        Assert.Null(typeof(LedgerEntryResponse).GetProperty("FreeTokensCovered"));
        Assert.Null(typeof(LedgerEntryResponse).GetProperty("RowVersion"));
    }

    [Fact]
    public async Task GetLedger_MapsIsFreeCovered_OnlyForFullyFreeSpendRows()
    {
        // Server owns the free-covered rule: fully free Spend → true; partial / pre-fix / TopUp → false.
        // Client must not re-derive from amountUsd === 0.
        var entries = new List<UsageLedgerEntry>
        {
            new()
            {
                Type = LedgerEntryType.Spend,
                CostUsd = 0m,
                FreeTokensCovered = 100,
                InputTokens = 40,
                OutputTokens = 60,
                Feature = "Tutoring:Guidance",
                TimestampUtc = DateTime.UtcNow
            },
            new()
            {
                Type = LedgerEntryType.Spend,
                CostUsd = 13m,
                FreeTokensCovered = 500,   // partial — still a paid Usage row
                InputTokens = 100,
                OutputTokens = 1700,
                Feature = "PromptLab:Evaluate",
                TimestampUtc = DateTime.UtcNow
            },
            new()
            {
                Type = LedgerEntryType.Spend,
                CostUsd = 0.42m,
                FreeTokensCovered = null,  // pre-fix row — age out of recent-N, never backfilled
                InputTokens = 40,
                OutputTokens = 60,
                Feature = "Tutoring:Guidance",
                TimestampUtc = DateTime.UtcNow
            },
            new()
            {
                Type = LedgerEntryType.TopUp,
                CostUsd = 10m,
                FreeTokensCovered = null,
                Feature = "Billing:TopUp",
                TimestampUtc = DateTime.UtcNow
            }
        };
        _billing.GetRecentLedgerAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(entries);

        var result = Assert.IsType<OkObjectResult>(await _controller.GetLedger(20, CancellationToken.None));
        var response = Assert.IsAssignableFrom<IEnumerable<LedgerEntryResponse>>(result.Value).ToList();

        Assert.True(response[0].IsFreeCovered);
        Assert.False(response[1].IsFreeCovered);
        Assert.False(response[2].IsFreeCovered);
        Assert.False(response[3].IsFreeCovered);
    }

    [Fact]
    public async Task GetLedger_ClampsTakeToUpperBound()
    {
        _billing.GetRecentLedgerAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new List<UsageLedgerEntry>());

        await _controller.GetLedger(take: 1000, CancellationToken.None);

        await _billing.Received(1).GetRecentLedgerAsync(100, Arg.Any<CancellationToken>());
    }

    // == Pack catalog == //

    [Fact]
    public async Task GetPacks_ReturnsOkBareArrayOfPacks()
    {
        _billing.GetPacksAsync(Arg.Any<CancellationToken>()).Returns(new List<CreditPack>
        {
            new() { PriceId = "price_1", Name = "Mini", Amount = 10m, Currency = "usd" }
        });

        var result = Assert.IsType<OkObjectResult>(await _controller.GetPacks(CancellationToken.None));
        var response = Assert.IsAssignableFrom<IEnumerable<PackResponse>>(result.Value).ToList();

        Assert.Single(response);
        Assert.Equal("price_1", response[0].PriceId);
        Assert.Equal("Mini", response[0].Name);
        Assert.Equal(10m, response[0].Amount);
        Assert.Equal("usd", response[0].Currency);
    }

    [Fact]
    public async Task GetPacks_EmptyCatalog_ReturnsOkEmptyArray()
    {
        _billing.GetPacksAsync(Arg.Any<CancellationToken>()).Returns(new List<CreditPack>());

        var result = Assert.IsType<OkObjectResult>(await _controller.GetPacks(CancellationToken.None));
        var response = Assert.IsAssignableFrom<IEnumerable<PackResponse>>(result.Value);

        Assert.Empty(response);
    }
}
