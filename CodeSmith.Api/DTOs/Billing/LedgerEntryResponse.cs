// == Ledger Entry Response DTO == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Api.DTOs.Billing;

/// <summary>
/// One customer-facing statement line — a top-up or a spend. Exposes the amount actually debited or
/// credited (CostUsd as AmountUsd) plus whether a Spend was fully free-covered. It never carries
/// ProviderCostUsd (raw cost / margin), FreeTokensCovered (token counts), or the RowVersion token.
/// </summary>
public class LedgerEntryResponse
{
    public LedgerEntryType Type { get; set; }        // TopUp (credit) or Spend (debit)
    public decimal AmountUsd { get; set; }           // Credited amount for a TopUp; amount actually debited for a Spend
    public bool IsFreeCovered { get; set; }          // True only when free tokens fully covered a Spend (render as "Free")
    public string? Feature { get; set; }             // e.g. "Tutoring:Guidance", "Billing:TopUp"
    public DateTime TimestampUtc { get; set; }
}
