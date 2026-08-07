// == Stripe Price Reader Interface (internal billing seam) == //
using Stripe;

namespace CodeSmith.Infrastructure.Billing;

/// <summary>
/// Internal seam over Stripe Price retrieval (with Product expand). Isolates the Stripe SDK so
/// StripeBillingService can unit-test pack-catalog filtering, ordering, and cache behavior without
/// live Stripe calls. Lives in Infrastructure because it returns a Stripe type; the public
/// IBillingService seam stays processor-agnostic.
/// </summary>
public interface IStripePriceReader
{
    // Retrieves a Price with product expanded. Throws StripeException on transport/API failure
    // (including resource_missing for an unknown price id).
    Task<Price> GetWithProductAsync(string priceId, CancellationToken ct = default);
}
