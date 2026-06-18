// == LLM Pricing Implementation == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace CodeSmith.Infrastructure.Services.Usage;

/// <summary>
/// In-memory pricing table. Lives in Infrastructure but rates are the source of truth for tests.
/// Free quota is separate (enforced by caller using FreeQuotaMax).
/// </summary>
public class LlmPricing : ILlmPricing
{
    // == Versioned rate table (per 1k tokens, USD) ==
    // Update when model names or published prices change.
    private static readonly IReadOnlyDictionary<(AiProvider Provider, string Model), (decimal InputPer1K, decimal OutputPer1K)> RateTable =
        new Dictionary<(AiProvider, string), (decimal, decimal)>
        {
            // Anthropic (current config names)
            [(AiProvider.Anthropic, "claude-haiku-4-5-20251001")] = (0.0008m, 0.004m),
            [(AiProvider.Anthropic, "claude-sonnet-4-6")]        = (0.003m, 0.015m),

            // OpenAI (current config names)
            [(AiProvider.OpenAi, "gpt-4.1-mini")] = (0.00015m, 0.0006m),
            [(AiProvider.OpenAi, "gpt-4.1")]      = (0.002m, 0.008m),

            // xAI (current config names)
            [(AiProvider.Xai, "grok-4.3")] = (0.002m, 0.010m),
        };

    private static readonly decimal HighestRatePer1K = 0.015m; // conservative global max for upper-bound estimates

    public decimal ComputeCostUsd(AiProvider provider, string model, int inputTokens, int outputTokens)
    {
        if (!RateTable.TryGetValue((provider, model), out var rates))
        {
            // Fallback to highest known rate for unknown model (safety)
            rates = (HighestRatePer1K, HighestRatePer1K);
        }

        var inputCost = (inputTokens / 1000m) * rates.InputPer1K;
        var outputCost = (outputTokens / 1000m) * rates.OutputPer1K;
        return inputCost + outputCost;
    }

    public decimal EstimateUpperBoundCost(AiProvider provider, int estInputTokens, int estOutputTokens)
    {
        // Lean approach: use global highest rate so we never need to know the exact model before the call
        var inputCost = (estInputTokens / 1000m) * HighestRatePer1K;
        var outputCost = (estOutputTokens / 1000m) * HighestRatePer1K;
        return inputCost + outputCost;
    }
}
