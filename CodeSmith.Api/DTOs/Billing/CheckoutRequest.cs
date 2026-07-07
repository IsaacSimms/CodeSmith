// == Checkout Request DTO == //
namespace CodeSmith.Api.DTOs.Billing;

/// <summary>
/// Request body for creating a Stripe checkout session. The priceId must be an allow-listed credit pack.
/// </summary>
public class CheckoutRequest
{
    public string PriceId { get; set; } = string.Empty;   // One of the configured purchasable Price IDs
}
