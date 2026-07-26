// == LLM Pricing Catalog Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Infrastructure.Services.Usage;

namespace CodeSmith.Tests.Infrastructure.Usage;

public class LlmPricingCatalogTests
{
    [Theory]
    [InlineData(AiProvider.Anthropic, "claude-sonnet-5")]
    [InlineData(AiProvider.Anthropic, "claude-haiku-4-5")]
    [InlineData(AiProvider.OpenAi, "gpt-4.1")]
    [InlineData(AiProvider.OpenAi, "gpt-4.1-mini")]
    [InlineData(AiProvider.Xai, "grok-4.5")]
    public void IsModelPriced_KnownModels_ReturnsTrue(AiProvider provider, string model)
        => Assert.True(LlmPricingCatalog.IsModelPriced(provider, model));

    [Theory]
    [InlineData(AiProvider.Anthropic, "made-up-model")]
    [InlineData(AiProvider.Xai, "claude-sonnet-5")]   // real model, wrong provider — must not match
    [InlineData(AiProvider.OpenAi, "")]
    [InlineData(AiProvider.OpenAi, "   ")]
    public void IsModelPriced_UnknownOrBlank_ReturnsFalse(AiProvider provider, string model)
        => Assert.False(LlmPricingCatalog.IsModelPriced(provider, model));

    [Fact]
    public void TryGetRate_KnownModel_ReturnsRate()
    {
        Assert.True(LlmPricingCatalog.TryGetRate(AiProvider.Anthropic, "claude-sonnet-5", out var rates));
        Assert.Equal(0.003m, rates.InputPer1K);
        Assert.Equal(0.015m, rates.OutputPer1K);
    }

    [Fact]
    public void TryGetRate_UnknownModel_ReturnsFalse()
        => Assert.False(LlmPricingCatalog.TryGetRate(AiProvider.Anthropic, "nope", out _));
}
