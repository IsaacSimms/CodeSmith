// == Billing Controller == //
using CodeSmith.Api.DTOs.Billing;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSmith.Api.Controllers;

/// <summary>
/// Prepaid-credits billing endpoints: create a Stripe checkout, receive completion webhooks, and read the
/// caller's balance, ledger, and pack catalog. The webhook is the only anonymous endpoint and is
/// signature-verified; all others require authentication. This controller writes credits only — it never debits.
/// </summary>
[ApiController]
[Route("api/billing")]
public class BillingController : ControllerBase
{
    private readonly IBillingService _billing;

    public BillingController(IBillingService billing)
    {
        _billing = billing;
    }

    // == Create Checkout Session == //

    [HttpPost("checkout")]
    [Authorize]
    [ProducesResponseType(typeof(CheckoutResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCheckout([FromBody] CheckoutRequest request, CancellationToken ct)
    {
        var url = await _billing.CreateCheckoutSessionAsync(request.PriceId, ct);
        return Ok(new CheckoutResponse { Url = url });
    }

    // == Webhook (anonymous, signature-verified, raw body) == //

    [HttpPost("webhook")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        // Signature verification hashes the exact bytes — must read the raw body, never a bound model.
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        var signature = Request.Headers["Stripe-Signature"].ToString();

        // Invalid signature → WebhookSignatureException → 400. A transient persistence failure propagates
        // → 500, so Stripe retries. Processed/duplicate/ignored all → 200 (do not ask Stripe to retry).
        var result = await _billing.HandleWebhookAsync(body, signature, ct);
        return Ok(new { result = result.ToString() });
    }

    // == Read: Balance == //

    [HttpGet("balance")]
    [Authorize]
    [ProducesResponseType(typeof(BalanceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBalance(CancellationToken ct)
    {
        var balance = await _billing.GetBalanceAsync(ct);
        return Ok(new BalanceResponse { PaidCreditsUsd = balance });
    }

    // == Read: Ledger == //

    [HttpGet("ledger")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<LedgerEntryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLedger([FromQuery] int take = 20, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 100);
        var entries = await _billing.GetRecentLedgerAsync(take, ct);

        var response = entries.Select(e => new LedgerEntryResponse
        {
            Type = e.Type,
            AmountUsd = e.CostUsd,     // ProviderCostUsd / FreeTokensCovered intentionally never projected
            // Fully free-covered Spend only; partial and pre-fix (null FreeTokensCovered) stay false.
            IsFreeCovered = e.Type == LedgerEntryType.Spend
                && e.FreeTokensCovered is int free
                && free > 0
                && free == e.InputTokens + e.OutputTokens,
            Feature = e.Feature,
            TimestampUtc = e.TimestampUtc
        });

        return Ok(response);
    }

    // == Read: Pack catalog == //

    [HttpGet("packs")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<PackResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetPacks(CancellationToken ct)
    {
        var packs = await _billing.GetPacksAsync(ct);

        // Bare JSON array — no wrapper object (pack-catalog contract).
        var response = packs.Select(p => new PackResponse
        {
            PriceId = p.PriceId,
            Name = p.Name,
            Amount = p.Amount,
            Currency = p.Currency
        });

        return Ok(response);
    }
}
