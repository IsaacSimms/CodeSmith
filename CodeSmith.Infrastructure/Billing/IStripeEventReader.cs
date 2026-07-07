// == Stripe Event Reader Interface (internal billing seam) == //
using Stripe;

namespace CodeSmith.Infrastructure.Billing;

/// <summary>
/// Internal seam over Stripe's signature verification. Isolates the static EventUtility.ConstructEvent call
/// so StripeBillingService can be unit-tested with a substituted reader instead of minting real HMAC
/// signatures. Lives in Infrastructure because it returns a Stripe type; the public IBillingService seam
/// stays processor-agnostic.
/// </summary>
public interface IStripeEventReader
{
    // Verifies the signature over the raw body and returns the parsed event. Throws StripeException on a
    // signature mismatch.
    Event Read(string requestBody, string signatureHeader, string webhookSecret);
}
