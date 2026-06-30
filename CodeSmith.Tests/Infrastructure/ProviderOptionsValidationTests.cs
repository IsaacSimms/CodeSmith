// == Provider Options Pricing Validation Tests == //
using CodeSmith.Infrastructure.Configuration;
using CodeSmith.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CodeSmith.Tests.Infrastructure;

public class ProviderOptionsValidationTests
{
    private static IServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCodeSmithInfrastructure(configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AnthropicOptions_WithUnpricedModel_FailsValidation()
    {
        var sp = BuildProvider(new Dictionary<string, string?>
        {
            ["Anthropic:AccurateModel"] = "totally-made-up-model",
            ["Anthropic:FastModel"]     = "claude-haiku-4-5-20251001",
        });

        // Validation runs on first IOptions<T>.Value access (and at host start via ValidateOnStart).
        var ex = Assert.Throws<OptionsValidationException>(() => sp.GetRequiredService<IOptions<AnthropicOptions>>().Value);
        Assert.Contains("AccurateModel", ex.Message);
    }

    [Fact]
    public void ProviderOptions_WithDefaultModels_ValidateSuccessfully()
    {
        var sp = BuildProvider(new Dictionary<string, string?>());

        // Defaults are all present in the pricing rate table, so validation passes.
        Assert.NotNull(sp.GetRequiredService<IOptions<AnthropicOptions>>().Value);
        Assert.NotNull(sp.GetRequiredService<IOptions<OpenAiOptions>>().Value);
        Assert.NotNull(sp.GetRequiredService<IOptions<XaiOptions>>().Value);
    }
}
