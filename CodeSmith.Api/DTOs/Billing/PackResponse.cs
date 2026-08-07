// == Pack Response DTO == //
namespace CodeSmith.Api.DTOs.Billing;

/// <summary>
/// Customer-facing credit pack from the catalog. Amount is decimal major units; currency is the ISO
/// code. Maps 1:1 from Core CreditPack — never carries Stripe types or margin data.
/// </summary>
public class PackResponse
{
    public string PriceId { get; set; } = string.Empty;  // Stripe Price id used at checkout
    public string Name { get; set; } = string.Empty;     // Display name (Stripe Product name)
    public decimal Amount { get; set; }                  // Major units (e.g. 10.00)
    public string Currency { get; set; } = string.Empty; // ISO code (usd in practice)
}
