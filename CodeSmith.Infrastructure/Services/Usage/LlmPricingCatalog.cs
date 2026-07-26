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
            // Anthropic ($3/$15 Sonnet 5, $1/$5 Haiku 4.5 per MTok). Sonnet 5 carries an introductory
            // $2/$10 rate through 2026-08-31; the standard rate is encoded so the table stays correct
            // when the promotion lapses — until then we simply over-recover on Anthropic traffic.
            [(AiProvider.Anthropic, "claude-haiku-4-5")] = (0.001m, 0.005m),
            [(AiProvider.Anthropic, "claude-sonnet-5")]  = (0.003m, 0.015m),

            // OpenAI ($2/$8 gpt-4.1, $0.40/$1.60 gpt-4.1-mini per MTok)
            [(AiProvider.OpenAi, "gpt-4.1-mini")] = (0.0004m, 0.0016m),
            [(AiProvider.OpenAi, "gpt-4.1")]      = (0.002m, 0.008m),

            // xAI. grok-4.5 is tiered by context size: $2/$6 per MTok at <=200k, $4/$12 above it.
            // The <=200k rate is encoded because editorContent caps at 50k chars (~12.5k tokens), so
            // requests stay an order of magnitude below the cliff. If a feature ever pushes past 200k,
            // margin on that call falls to zero (the 2x markup exactly absorbs the 2x rate jump) — it
            // never goes negative, but this table needs a context dimension before that becomes routine.
            [(AiProvider.Xai, "grok-4.5")] = (0.002m, 0.006m),
        };

    public static bool TryGetRate(AiProvider provider, string model, out (decimal InputPer1K, decimal OutputPer1K) rates)
        => RateTable.TryGetValue((provider, model), out rates);

    public static bool IsModelPriced(AiProvider provider, string model)
        => !string.IsNullOrWhiteSpace(model) && RateTable.ContainsKey((provider, model));
}
