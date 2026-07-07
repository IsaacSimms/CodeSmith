// == Balance Response DTO == //
namespace CodeSmith.Api.DTOs.Billing;

/// <summary>
/// Customer-facing paid-credit balance. Deliberately excludes internal fields (RowVersion, free-window
/// bookkeeping) and any provider-cost/margin data.
/// </summary>
public class BalanceResponse
{
    public decimal PaidCreditsUsd { get; set; }   // Remaining prepaid credit in USD
}
