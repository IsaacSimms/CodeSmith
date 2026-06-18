// == LLM Pricing Interface == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Provides cost calculation for usage enforcement (pass-through + markup ready).
/// Rates are versioned inside the implementation for testability.
/// </summary>
public interface ILlmPricing
{
    decimal ComputeCostUsd(AiProvider provider, string model, int inputTokens, int outputTokens);

    // Conservative upper bound for pre-check using highest rate (lean approach — no per-model resolution before call)
    decimal EstimateUpperBoundCost(AiProvider provider, int estInputTokens, int estOutputTokens);
}
