// == LLM Pricing Tests (TDD) == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Infrastructure.Services.Usage;

namespace CodeSmith.Tests.Infrastructure.Usage;

public class LlmPricingTests
{
    private readonly ILlmPricing _pricing = new LlmPricing();

    [Fact]
    public void ComputeCostUsd_KnownModel_ReturnsPreciseCost()
    {
        // Haiku example: 1000 in + 500 out
        var cost = _pricing.ComputeCostUsd(AiProvider.Anthropic, "claude-haiku-4-5-20251001", 1000, 500);

        Assert.Equal(0.0008m + 0.002m, cost); // 0.8 + 2.0 = 2.8 milli
    }

    [Fact]
    public void ComputeCostUsd_UnknownModel_FallsBackToHighestRate()
    {
        var cost = _pricing.ComputeCostUsd(AiProvider.Anthropic, "future-model-999", 1000, 1000);

        // HighestRatePer1K = 0.015
        Assert.Equal(0.015m + 0.015m, cost);
    }

    [Fact]
    public void EstimateUpperBoundCost_UsesHighestRate_IgnoresModel()
    {
        var upper = _pricing.EstimateUpperBoundCost(AiProvider.OpenAi, 2000, 1000);

        Assert.Equal(0.045m, upper); // 3k tokens * 0.015
    }

    [Theory]
    [InlineData(AiProvider.Anthropic, "claude-sonnet-4-6", 1000, 0, 0.003)]
    [InlineData(AiProvider.Xai, "grok-4.3", 0, 2000, 0.020)]
    public void ComputeCostUsd_VariousModels_MatchTable(AiProvider provider, string model, int inTok, int outTok, decimal expected)
    {
        var cost = _pricing.ComputeCostUsd(provider, model, inTok, outTok);
        Assert.Equal(expected, cost);
    }
}
