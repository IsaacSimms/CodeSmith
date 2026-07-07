// == Stripe Event Reader == //
using Stripe;

namespace CodeSmith.Infrastructure.Billing;

/// <summary>
/// Real event reader that delegates to Stripe's EventUtility for signature verification and parsing.
/// </summary>
public class StripeEventReader : IStripeEventReader
{
    public Event Read(string requestBody, string signatureHeader, string webhookSecret)
        => EventUtility.ConstructEvent(requestBody, signatureHeader, webhookSecret);
}
