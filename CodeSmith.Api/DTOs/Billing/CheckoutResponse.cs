// == Checkout Response DTO == //
namespace CodeSmith.Api.DTOs.Billing;

/// <summary>
/// Response for a created checkout session: the hosted Stripe URL to redirect the buyer to.
/// </summary>
public class CheckoutResponse
{
    public string Url { get; set; } = string.Empty;   // Stripe-hosted checkout page URL
}
