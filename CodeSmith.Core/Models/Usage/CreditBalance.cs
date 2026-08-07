// == Credit Balance Entity == //
using System.ComponentModel.DataAnnotations;

namespace CodeSmith.Core.Models.Usage;

/// <summary>
/// Per-user balance for the free token grant + prepaid credits.
/// Strong consistency enforced at the database/row level for checks.
/// The free grant is one-time per objectId: it never expires and never resets, so the row carries
/// no wall-clock field — headroom is always <c>FreeQuotaMax − FreeTokensUsed</c>.
/// </summary>
public class CreditBalance
{
    [Key]
    public string ObjectId { get; set; } = string.Empty;           // Entra External ID objectId (stable user identifier)

    public decimal PaidCreditsBalance { get; set; }                 // Purchased credits (treated in USD-equivalent for pass-through + markup)

    public long FreeTokensUsed { get; set; }                        // Tokens consumed against this account's free grant

    public long FreeQuotaMax { get; set; } = 20_000;                // Size of the one-time free grant, snapshotted per row (default; seeded from UsageOptions)

    [Timestamp]
    public byte[]? RowVersion { get; set; }                         // Optimistic concurrency token for safe concurrent deducts

    // Canonical seed for a brand-new balance. Single source of the creation defaults shared by usage
    // enforcement and billing top-ups so the two paths cannot drift.
    public static CreditBalance CreateNew(string objectId, long freeQuotaMax)
        => new() { ObjectId = objectId, FreeQuotaMax = freeQuotaMax };
}
