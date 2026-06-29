// == Usage Enforcer Interface == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// The usage enforcement seam. Must be called before every LLM spend and after success.
/// Check uses strong consistency. Record writes actuals and deducts.
/// </summary>
public interface IUsageEnforcer
{
    /// <summary>
    /// Checks quota/credits using upper-bound estimate. Throws InsufficientQuotaException if not sufficient.
    /// clientIp (if provided) is used for the per-IP aggregate cap (60k).
    /// Returns true if this call will consume free quota (for tier downgrade decisions in the decorator).
    /// </summary>
    Task<bool> CheckAndReserveAsync(
        string objectId,
        string? clientIp,
        AiProvider provider,
        int estInputTokens,
        int estOutputTokens,
        CancellationToken ct = default);

    /// <summary>
    /// Records the actual usage (using model + tokens from the LLM response) and deducts the precise charge.
    /// chargeUsd is debited from paid credits and recorded as CostUsd; providerCostUsd is the raw provider cost,
    /// recorded for margin reporting. Free quota is consumed before paid credits. clientIp is used for the per-IP aggregate.
    /// </summary>
    Task RecordActualAsync(
        string objectId,
        string? clientIp,
        AiProvider provider,
        string model,
        int actualInput,
        int actualOutput,
        decimal chargeUsd,
        decimal providerCostUsd,
        string? feature = null,
        CancellationToken ct = default);
}
