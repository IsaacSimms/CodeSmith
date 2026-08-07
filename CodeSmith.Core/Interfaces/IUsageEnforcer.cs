// == Usage Enforcer Interface == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Models.Usage;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// The usage enforcement seam: reserve → settle / release for spend, plus a lock-free free-quota
/// read for the account surface. <c>ReserveAsync</c> writes an upper-bound hold under the per-user
/// lock and throws <c>InsufficientQuotaException</c> when the user is not covered; <c>SettleAsync</c>
/// reverses the hold and applies the real cost (plus the ledger entry); <c>ReleaseAsync</c> refunds
/// the hold when the LLM call never produced a billable result. <c>GetQuotaAsync</c> reports the same
/// free-headroom inputs the gate uses (grant used/max + IP constraint) without taking
/// <c>IUserUsageLock</c> or creating a balance row. Persisting the hold at reserve time is what makes
/// concurrent completions for one user (the Prompt Lab simulate/evaluate fan-out) serialize correctly
/// instead of all passing the same gate against an unchanged balance.
/// </summary>
public interface IUsageEnforcer
{
    // Holds an upper-bound estimate against the user's remaining free grant / per-IP aggregate / paid
    // credits. Throws InsufficientQuotaException if none of those can cover the call.
    Task<UsageReservation> ReserveAsync(
        string objectId,
        string? clientIp,
        AiProvider provider,
        int estInputTokens,
        int estOutputTokens,
        CancellationToken ct = default);

    // Reverses the hold and deducts the actual cost (free quota first, then paid credits), then appends
    // the ledger entry. The prorated debit lands in both PaidCreditsBalance and CostUsd (one local);
    // free tokens applied are stored as FreeTokensCovered. providerCostUsd is the raw provider cost.
    Task SettleAsync(
        UsageReservation reservation,
        string model,
        int actualInput,
        int actualOutput,
        decimal chargeUsd,
        decimal providerCostUsd,
        string? feature = null,
        CancellationToken ct = default);

    // Reverses the hold with no ledger entry — used when the LLM call failed, so it consumes no quota.
    Task ReleaseAsync(UsageReservation reservation, CancellationToken ct = default);

    // Lock-free free-quota read for UI. Never creates a CreditBalance row; a missing row synthesizes
    // freeTokensUsed=0 and freeQuotaMax from UsageOptions. Reports persisted state including holds.
    Task<QuotaSnapshot> GetQuotaAsync(string objectId, string? clientIp, CancellationToken ct = default);
}
