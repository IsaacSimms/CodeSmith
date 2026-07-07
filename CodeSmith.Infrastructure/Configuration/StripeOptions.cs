// == Stripe Billing Configuration Options == //
namespace CodeSmith.Infrastructure.Configuration;

/// <summary>
/// Configuration for the Stripe prepaid-credits billing module.
/// Binds to the "Stripe" section in appsettings. Secrets (SecretKey, WebhookSecret) come from
/// Key Vault / user-secrets and must never be hardcoded.
/// </summary>
public class StripeOptions
{
    public const string SectionName = "Stripe";

    public string SecretKey { get; set; } = string.Empty;      // sk_test_... / sk_live_... — from Key Vault
    public string WebhookSecret { get; set; } = string.Empty;  // whsec_... signing secret for webhook verification

    // Allow-list of purchasable Price IDs. Checkout rejects any priceId not in this set.
    public string[] PriceIds { get; set; } = Array.Empty<string>();

    public string SuccessUrl { get; set; } = string.Empty;     // Redirect target after a completed checkout
    public string CancelUrl { get; set; } = string.Empty;      // Redirect target after a cancelled checkout
}
