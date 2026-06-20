// == Credit Balance Entity == //
using System.ComponentModel.DataAnnotations;

namespace CodeSmith.Core.Models.Usage;

/// <summary>
/// Per-user balance for free quota (time-boxed window) + prepaid credits.
/// Strong consistency enforced at the database/row level for checks.
/// Free quota is available only for the first 48 hours after first sighting of the objectId.
/// </summary>
public class CreditBalance
{
    [Key]
    public string ObjectId { get; set; } = string.Empty;           // Entra External ID objectId (stable user identifier)

    public decimal PaidCreditsBalance { get; set; }                 // Purchased credits (treated in USD-equivalent for pass-through + markup)

    public long FreeTokensUsedInWindow { get; set; }                // Tokens consumed against the free window quota

    public long FreeQuotaMax { get; set; } = 20_000;                // Configurable hard cap for free tier (default; overridable via UsageOptions)

    public DateTime FirstSeenUtc { get; set; } = DateTime.UtcNow;   // Start of the 48h free window for this objectId (global)

    [Timestamp]
    public byte[]? RowVersion { get; set; }                         // Optimistic concurrency token for safe concurrent deducts
}
