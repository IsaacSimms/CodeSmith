// == Credit Balance Entity == //
using System.ComponentModel.DataAnnotations;

namespace CodeSmith.Core.Models.Usage;

/// <summary>
/// Per-user balance for free monthly quota + prepaid credits.
/// Strong consistency enforced at the database/row level for checks.
/// </summary>
public class CreditBalance
{
    [Key]
    public string ObjectId { get; set; } = string.Empty;           // Entra External ID objectId (stable user identifier)

    public decimal PaidCreditsBalance { get; set; }                 // Purchased credits (treated in USD-equivalent for pass-through + markup)

    public long FreeTokensUsedThisMonth { get; set; }               // Tokens consumed against the free monthly quota

    public long FreeQuotaMax { get; set; } = 100_000;               // Configurable hard cap for free tier (default; overridable via UsageOptions)

    public DateTime LastFreeResetUtc { get; set; } = DateTime.UtcNow; // Used to detect monthly rollover and reset FreeTokensUsedThisMonth

    [Timestamp]
    public byte[]? RowVersion { get; set; }                         // Optimistic concurrency token for safe concurrent deducts
}
