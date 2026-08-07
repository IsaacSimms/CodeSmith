// == AiOptions Startup Validation Tests == //
using CodeSmith.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CodeSmith.Tests.Infrastructure;

/// <summary>
/// Pins that a malformed Ai:ActiveProvider fails at options validation (host start),
/// not later at request time when a client omits provider.
/// </summary>
public class AiOptionsValidationTests
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
    public void ActiveProvider_WithUnknownName_FailsAtOptionsAccess()
    {
        var sp = BuildProvider(new Dictionary<string, string?>
        {
            ["Ai:ActiveProvider"] = "Grok",   // not an AiProvider member
        });

        // Binder rejects unknown names when options are first resolved (ValidateOnStart does this at host start)
        var ex = Assert.Throws<InvalidOperationException>(
            () => sp.GetRequiredService<IOptions<CodeSmith.Infrastructure.Configuration.AiOptions>>().Value);
        Assert.Contains("ActiveProvider", ex.Message);
    }

    [Fact]
    public void ActiveProvider_WithValidName_ValidatesSuccessfully()
    {
        var sp = BuildProvider(new Dictionary<string, string?>
        {
            ["Ai:ActiveProvider"] = "Xai",
        });

        Assert.Equal(
            CodeSmith.Core.Enums.AiProvider.Xai,
            sp.GetRequiredService<IOptions<CodeSmith.Infrastructure.Configuration.AiOptions>>().Value.ActiveProvider);
    }
}
