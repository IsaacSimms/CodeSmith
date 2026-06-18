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

    public AiProvider Provider { get; set; }

    public string Model { get; set; } = string.Empty;      // Exact model used

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public decimal CostUsd { get; set; }                   // Computed using ILlmPricing at record time

    public string? Feature { get; set; }                   // e.g. "Tutoring:Guidance", "PromptLab:Evaluate" (simple string per decision)

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
