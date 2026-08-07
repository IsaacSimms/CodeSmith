// == Credit Pack (catalog entry) == //
namespace CodeSmith.Core.Models.Usage;

/// <summary>
/// A purchasable prepaid credit pack as returned by the pack-catalog endpoint. Amount is decimal major
/// units (e.g. 10.00 USD), never Stripe minor units. Currency is the ISO code; non-USD packs are filtered
/// out before they reach callers.
/// </summary>
public sealed class CreditPack
{
    public required string PriceId { get; init; }   // Stripe Price id (allow-listed)
    public required string Name { get; init; }      // Stripe Product name (display)
    public decimal Amount { get; init; }            // Major units (amount_total / 100)
    public required string Currency { get; init; }  // ISO code, always "usd" in practice
}
