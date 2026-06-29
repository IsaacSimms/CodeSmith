// == LLM Pricing Implementation == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CodeSmith.Infrastructure.Services.Usage;

/// <summary>
/// Cost engine for the usage/credit seam. The rate table holds true provider cost (what we pay the
/// provider); a config-driven markup multiplier turns that into the customer-facing charge. Keeping
/// cost and charge as distinct methods lets the ledger record both, so margin stays reportable.
/// </summary>
public class LlmPricing : ILlmPricing
{
    // == Versioned rate table (raw provider cost per 1k tokens, USD) ==
    // Update when model names or published provider prices change. Markup is applied separately.
    private static readonly IReadOnlyDictionary<(AiProvider Provider, string Model), (decimal InputPer1K, decimal OutputPer1K)> RateTable =
        new Dictionary<(AiProvider, string), (decimal, decimal)>
        {
            // Anthropic ($3/$15 Sonnet, $1/$5 Haiku per MTok)
            [(AiProvider.Anthropic, "claude-haiku-4-5-20251001")] = (0.001m, 0.005m),
            [(AiProvider.Anthropic, "claude-sonnet-4-6")]         = (0.003m, 0.015m),

            // OpenAI ($2/$8 gpt-4.1, $0.40/$1.60 gpt-4.1-mini per MTok)
            [(AiProvider.OpenAi, "gpt-4.1-mini")] = (0.0004m, 0.0016m),
            [(AiProvider.OpenAi, "gpt-4.1")]      = (0.002m, 0.008m),

            // xAI ($1.25/$2.50 grok-4.3 per MTok)
            [(AiProvider.Xai, "grok-4.3")] = (0.00125m, 0.0025m),
        };

    private static readonly decimal HighestRatePer1K = 0.015m; // conservative global max for upper-bound estimates

    private readonly decimal _markup;

    public LlmPricing(IOptions<UsageOptions> options)
    {
        _markup = options.Value.PaidMarkupMultiplier;
    }

    // == Raw provider cost == //

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

    // == Customer charge (raw cost × markup) == //

    public decimal ComputeChargeUsd(AiProvider provider, string model, int inputTokens, int outputTokens)
        => ComputeCostUsd(provider, model, inputTokens, outputTokens) * _markup;

    // == Pre-call upper bound on the charge == //

    public decimal EstimateUpperBoundCost(AiProvider provider, int estInputTokens, int estOutputTokens)
    {
        // Lean approach: use global highest rate so we never need to know the exact model before the call.
        // Markup is applied so the gate reserves against the charge, not the raw cost.
        var inputCost = (estInputTokens / 1000m) * HighestRatePer1K;
        var outputCost = (estOutputTokens / 1000m) * HighestRatePer1K;
        return (inputCost + outputCost) * _markup;
    }
}
