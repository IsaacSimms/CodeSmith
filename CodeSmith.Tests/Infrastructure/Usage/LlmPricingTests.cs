// == LLM Pricing Tests (TDD) == //
using CodeSmith.Core.Enums;
using CodeSmith.Infrastructure.Configuration;
using CodeSmith.Infrastructure.Services.Usage;
using Microsoft.Extensions.Options;

namespace CodeSmith.Tests.Infrastructure.Usage;

public class LlmPricingTests
{
    // Raw cost is markup-independent; pass a non-1 markup to prove ComputeCostUsd ignores it.
    private static LlmPricing BuildPricing(decimal markup = 1.0m)
        => new(Options.Create(new UsageOptions { PaidMarkupMultiplier = markup }));

    [Fact]
    public void ComputeCostUsd_KnownModel_ReturnsRawProviderCost()
    {
        // Haiku 4.5: $1/$5 per MTok → 0.001/0.005 per 1k. 1000 in + 500 out.
        var cost = BuildPricing(markup: 2.0m).ComputeCostUsd(AiProvider.Anthropic, "claude-haiku-4-5-20251001", 1000, 500);

        Assert.Equal(0.001m + 0.0025m, cost); // raw, unaffected by markup
    }

    [Fact]
    public void ComputeCostUsd_UnknownModel_FallsBackToHighestRate()
    {
        var cost = BuildPricing().ComputeCostUsd(AiProvider.Anthropic, "future-model-999", 1000, 1000);

        // HighestRatePer1K = 0.015
        Assert.Equal(0.015m + 0.015m, cost);
    }

    [Fact]
    public void ComputeChargeUsd_AppliesMarkupToRawCost()
    {
        var pricing = BuildPricing(markup: 2.0m);

        var raw    = pricing.ComputeCostUsd(AiProvider.Anthropic, "claude-haiku-4-5-20251001", 1000, 500);
        var charge = pricing.ComputeChargeUsd(AiProvider.Anthropic, "claude-haiku-4-5-20251001", 1000, 500);

        Assert.Equal(raw * 2.0m, charge);
    }

    [Fact]
    public void EstimateUpperBoundCost_AppliesMarkup_IgnoresModel()
    {
        var upper = BuildPricing(markup: 2.0m).EstimateUpperBoundCost(AiProvider.OpenAi, 2000, 1000);

        Assert.Equal(0.045m * 2.0m, upper); // 3k tokens * 0.015 * markup
    }

    [Theory]
    [InlineData(AiProvider.Anthropic, "claude-sonnet-4-6", 1000, 0, 0.003)]
    [InlineData(AiProvider.Xai, "grok-4.3", 0, 2000, 0.005)]           // $2.50/MTok out → 0.0025/1k × 2k
    [InlineData(AiProvider.OpenAi, "gpt-4.1-mini", 1000, 1000, 0.002)] // 0.0004 + 0.0016
    public void ComputeCostUsd_VariousModels_MatchCorrectedTable(AiProvider provider, string model, int inTok, int outTok, decimal expected)
    {
        var cost = BuildPricing().ComputeCostUsd(provider, model, inTok, outTok);
        Assert.Equal(expected, cost);
    }
}
