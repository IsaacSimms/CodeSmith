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

    // Customer-facing notional charge = raw cost × markup. SettleAsync prorates this by the paid-token
    // fraction into the actual debit (CostUsd / PaidCreditsBalance); free coverage yields $0.
    decimal ComputeChargeUsd(AiProvider provider, string model, int inputTokens, int outputTokens);

    // Conservative upper bound on the customer charge for the pre-call gate (highest rate × markup — no per-model resolution before call)
    decimal EstimateUpperBoundCost(AiProvider provider, int estInputTokens, int estOutputTokens);
}
