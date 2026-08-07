// == Billing Service Interface == //
using CodeSmith.Core.Models.Usage;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Provider-agnostic billing surface for prepaid credits. Billing only writes credits (via checkout +
/// webhook) and reads balance/ledger; it never debits — usage enforcement owns all deductions. Carries no
/// payment-provider types so the seam stays independent of the concrete processor.
/// </summary>
public interface IBillingService
{
    // Creates a checkout session for the authenticated caller and returns the URL to redirect them to.
    // Throws InvalidPriceException if priceId is not an allow-listed pack.
    Task<string> CreateCheckoutSessionAsync(string priceId, CancellationToken ct = default);

    // Verifies and processes a raw webhook payload. Idempotent. Throws WebhookSignatureException on an
    // invalid signature (mapped to 400); a transient persistence failure propagates (mapped to 500) so the
    // processor retries.
    Task<WebhookResult> HandleWebhookAsync(string requestBody, string signatureHeader, CancellationToken ct = default);

    // Current paid-credit balance (USD) for the authenticated caller; zero if no balance exists yet.
    Task<decimal> GetBalanceAsync(CancellationToken ct = default);

    // Most-recent ledger rows (top-ups and spends) for the authenticated caller.
    Task<IReadOnlyList<UsageLedgerEntry>> GetRecentLedgerAsync(int take, CancellationToken ct = default);

    // Allow-listed credit packs from the payment provider, in StripeOptions.PriceIds order. Unusable
    // entries (missing, inactive, non-USD, blank Product name) are skipped. Throws BillingServiceException
    // when the provider is unreachable so the purchase section can 502 independently.
    Task<IReadOnlyList<CreditPack>> GetPacksAsync(CancellationToken ct = default);
}

public enum WebhookResult
{
    Credited = 0,          // A top-up was applied for this event
    AlreadyProcessed = 1,  // Event was seen before; no change (safe replay)
    Ignored = 2            // Event was not a completed checkout, or failed a guard (e.g. non-USD)
}
