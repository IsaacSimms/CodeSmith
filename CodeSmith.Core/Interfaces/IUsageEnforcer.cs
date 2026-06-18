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
    /// </summary>
    Task CheckAndReserveAsync(
        string objectId,
        AiProvider provider,
        int estInputTokens,
        int estOutputTokens,
        CancellationToken ct = default);

    /// <summary>
    /// Records the actual usage (using model + tokens from the LLM response) and deducts the precise cost.
    /// Free quota is consumed before paid credits.
    /// </summary>
    Task RecordActualAsync(
        string objectId,
        AiProvider provider,
        string model,
        int actualInput,
        int actualOutput,
        decimal costUsd,
        string? feature = null,
        CancellationToken ct = default);
}
