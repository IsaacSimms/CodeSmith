// == LLM Pricing Interface == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Provides cost calculation for usage enforcement (pass-through + markup ready).
/// Rates are versioned inside the implementation for testability.
/// </summary>
public interface ILlmPricing
{
    // Raw provider cost (what we pay the provider). Recorded as ProviderCostUsd; basis for margin reporting.
    decimal ComputeCostUsd(AiProvider provider, string model, int inputTokens, int outputTokens);

    // Customer-facing charge = raw cost × markup. This is what's debited from PaidCreditsBalance and recorded as CostUsd.
    decimal ComputeChargeUsd(AiProvider provider, string model, int inputTokens, int outputTokens);

    // Conservative upper bound on the customer charge for the pre-call gate (highest rate × markup — no per-model resolution before call)
    decimal EstimateUpperBoundCost(AiProvider provider, int estInputTokens, int estOutputTokens);
}
