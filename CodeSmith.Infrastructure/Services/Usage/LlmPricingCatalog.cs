// == LLM Pricing Catalog == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Infrastructure.Services.Usage;

/// <summary>
/// The single source of truth binding model identity to raw provider cost. Both <see cref="LlmPricing"/>
/// (runtime costing) and the startup options validation (which fails fast on a configured model that has
/// no rate) read this table, so the configured model names and the priced model names cannot silently
/// drift apart. Update the table when model names or published provider prices change; markup is applied
/// separately by <see cref="LlmPricing"/>.
/// </summary>
internal static class LlmPricingCatalog
{
    // Conservative global max, used for upper-bound pre-call estimates and the unknown-model fallback.
    public const decimal HighestRatePer1K = 0.015m;

    // Versioned rate table: raw provider cost per 1k tokens, USD.
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

    public static bool TryGetRate(AiProvider provider, string model, out (decimal InputPer1K, decimal OutputPer1K) rates)
        => RateTable.TryGetValue((provider, model), out rates);

    public static bool IsModelPriced(AiProvider provider, string model)
        => !string.IsNullOrWhiteSpace(model) && RateTable.ContainsKey((provider, model));
}
