// == Provider Options Pricing Validation Tests == //
using CodeSmith.Infrastructure.Configuration;
using CodeSmith.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CodeSmith.Tests.Infrastructure;

public class ProviderOptionsValidationTests
{
    private static IServiceProvider BuildProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCodeSmithInfrastructure(configuration);
        return services.BuildServiceProvider();
    }

    private static IServiceProvider BuildProvider(Dictionary<string, string?> settings)
        => BuildProvider(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

    [Fact]
    public void AnthropicOptions_WithUnpricedModel_FailsValidation()
    {
        var sp = BuildProvider(new Dictionary<string, string?>
        {
            ["Anthropic:AccurateModel"] = "totally-made-up-model",
            ["Anthropic:FastModel"]     = "claude-haiku-4-5",
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

    // == Shipped configuration guard == //

    // The fact above pins the C# defaults; this one pins the file that actually ships in the image.
    // Bumping a model in appsettings.json without adding its rate to LlmPricingCatalog otherwise fails
    // nowhere until ValidateOnStart aborts the container boot in Azure — this moves that failure into CI.
    // appsettings.Development.json is deliberately NOT layered: .gitignore excludes it, so layering it
    // would make the guard assert less on CI than it does locally.
    [Fact]
    public void ShippedAppSettings_ConfiguredModels_ArePricedInCatalog()
    {
        // appsettings.json reaches the test output directory via the CodeSmith.Api ProjectReference.
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var sp = BuildProvider(configuration);

        // Resolving .Value runs the same validation ValidateOnStart runs at host start.
        Assert.NotNull(sp.GetRequiredService<IOptions<AnthropicOptions>>().Value);
        Assert.NotNull(sp.GetRequiredService<IOptions<OpenAiOptions>>().Value);
        Assert.NotNull(sp.GetRequiredService<IOptions<XaiOptions>>().Value);
    }
}
