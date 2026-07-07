// == Ledger Entry Response DTO == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Api.DTOs.Billing;

/// <summary>
/// One customer-facing statement line — a top-up or a spend. Exposes only the charged/credited amount
/// (CostUsd); it never carries ProviderCostUsd (raw provider cost / margin) or the RowVersion token.
/// </summary>
public class LedgerEntryResponse
{
    public LedgerEntryType Type { get; set; }        // TopUp (credit) or Spend (debit)
    public decimal AmountUsd { get; set; }           // Credited amount for a TopUp, charged amount for a Spend
    public string? Feature { get; set; }             // e.g. "Tutoring:Guidance", "Billing:TopUp"
    public DateTime TimestampUtc { get; set; }
}
