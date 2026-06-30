// == LLM Pricing Implementation == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeSmith.Infrastructure.Services.Usage;

/// <summary>
/// Cost engine for the usage/credit seam. Reads true provider cost from <see cref="LlmPricingCatalog"/>
/// (the single model↔rate source); a config-driven markup multiplier turns that into the customer-facing
/// charge. Keeping cost and charge as distinct methods lets the ledger record both, so margin stays
/// reportable.
/// </summary>
public class LlmPricing : ILlmPricing
{
    private readonly decimal _markup;
    private readonly ILogger<LlmPricing> _logger;

    public LlmPricing(IOptions<UsageOptions> options, ILogger<LlmPricing> logger)
    {
        _markup = options.Value.PaidMarkupMultiplier;
        _logger = logger;
    }

    // == Raw provider cost == //

    public decimal ComputeCostUsd(AiProvider provider, string model, int inputTokens, int outputTokens)
    {
        if (!LlmPricingCatalog.TryGetRate(provider, model, out var rates))
        {
            // Should be unreachable after startup validation (adapters stamp LlmResponse.Model with the
            // configured name). Logged loudly because reaching it means model/rate-table drift; the
            // ceiling rate over-charges-safe rather than under-charging.
            _logger.LogWarning(
                "No pricing rate for {Provider}/{Model}; falling back to ceiling {Rate}/1k. Indicates model/rate-table drift.",
                provider, model, LlmPricingCatalog.HighestRatePer1K);
            rates = (LlmPricingCatalog.HighestRatePer1K, LlmPricingCatalog.HighestRatePer1K);
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
        // Lean approach: use the global highest rate so we never need the exact model before the call.
        // Markup is applied so the gate reserves against the charge, not the raw cost.
        var inputCost = (estInputTokens / 1000m) * LlmPricingCatalog.HighestRatePer1K;
        var outputCost = (estOutputTokens / 1000m) * LlmPricingCatalog.HighestRatePer1K;
        return (inputCost + outputCost) * _markup;
    }
}
