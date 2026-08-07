// == Usage Ledger Entry Entity == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Core.Models.Usage;

/// <summary>
/// Immutable append-only record of every LLM call for auditing and cost attribution.
/// </summary>
public class UsageLedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string ObjectId { get; set; } = string.Empty;   // Entra objectId

    public LedgerEntryType Type { get; set; }              // Spend (LLM call, default) or TopUp (Stripe credit purchase)

    public AiProvider? Provider { get; set; }              // Null for TopUp rows (no AI provider)

    public string? Model { get; set; }                     // Exact model used; null for TopUp rows

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public decimal CostUsd { get; set; }                   // Spend: amount actually debited from PaidCreditsBalance ($0 when free-covered). TopUp: amount credited.

    public decimal? ProviderCostUsd { get; set; }          // Raw provider cost (what we pay the provider); nullable for rows written before this column existed

    public int? FreeTokensCovered { get; set; }            // Free tokens applied on this Spend; null = written before this column existed (same convention as ProviderCostUsd)

    public string? Feature { get; set; }                   // e.g. "Tutoring:Guidance", "PromptLab:Evaluate" (simple string per decision)

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
