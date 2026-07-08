// == LLM Service Factory / Keyed Registration Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Infrastructure.DependencyInjection;
using CodeSmith.Infrastructure.Services;
using CodeSmith.Infrastructure.Services.Usage.Decorators;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodeSmith.Tests.Infrastructure;

/// <summary>
/// Pins the two-layer keyed LLM registration through the real composition root: the factory resolves
/// the usage-enforcing decorator for every provider, the raw adapters underneath are the expected
/// types, and the lifetimes hold (raw adapters singleton, decorators scoped — the captive-DbContext fix).
/// </summary>
public class LlmServiceFactoryTests
{
    // Mirrors the private RawKey(...) convention in ServiceCollectionExtensions — the registration
    // shape under test, so a key-format change must consciously update this test.
    private static string RawKey(AiProvider provider) => $"raw:{provider}";

    private static ServiceProvider BuildProvider()
    {
        // OpenAI/xAI adapters require a non-empty ApiKey at construction (ApiKeyCredential rejects
        // empty strings); Anthropic does not. Dummy keys keep the raw layer constructible.
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenAi:ApiKey"] = "test-key",
            ["Xai:ApiKey"]    = "test-key"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCodeSmithInfrastructure(configuration);
        return services.BuildServiceProvider();
    }

    // == Factory resolves the usage-enforcing decorator for every provider == //

    [Theory]
    [InlineData(AiProvider.Anthropic)]
    [InlineData(AiProvider.OpenAi)]
    [InlineData(AiProvider.Xai)]
    public void Get_EveryProvider_ReturnsUsageEnforcingDecorator(AiProvider provider)
    {
        using var sp = BuildProvider();
        using var scope = sp.CreateScope();

        var factory = scope.ServiceProvider.GetRequiredService<ILlmServiceFactory>();

        Assert.IsType<UsageEnforcingLlmService>(factory.Get(provider));
    }

    [Fact]
    public void Get_UnknownProvider_Throws()
    {
        using var sp = BuildProvider();
        using var scope = sp.CreateScope();

        var factory = scope.ServiceProvider.GetRequiredService<ILlmServiceFactory>();

        Assert.Throws<InvalidOperationException>(() => factory.Get((AiProvider)999));
    }

    // == Raw layer: expected adapter type per provider == //

    [Fact]
    public void RawRegistrations_ResolveExpectedAdapterTypes()
    {
        using var sp = BuildProvider();

        Assert.IsType<AnthropicLlmService>(sp.GetRequiredKeyedService<ILlmService>(RawKey(AiProvider.Anthropic)));
        Assert.IsType<OpenAiCompatibleLlmService>(sp.GetRequiredKeyedService<ILlmService>(RawKey(AiProvider.OpenAi)));
        Assert.IsType<OpenAiCompatibleLlmService>(sp.GetRequiredKeyedService<ILlmService>(RawKey(AiProvider.Xai)));
    }

    // == Lifetimes: raw singleton, decorator scoped == //

    [Fact]
    public void RawAdapter_IsSingletonAcrossScopes()
    {
        using var sp = BuildProvider();
        using var scope1 = sp.CreateScope();
        using var scope2 = sp.CreateScope();

        var first  = scope1.ServiceProvider.GetRequiredKeyedService<ILlmService>(RawKey(AiProvider.Anthropic));
        var second = scope2.ServiceProvider.GetRequiredKeyedService<ILlmService>(RawKey(AiProvider.Anthropic));

        Assert.Same(first, second);
    }

    [Fact]
    public void Decorator_IsScoped_FreshPerScope_SharedWithinScope()
    {
        using var sp = BuildProvider();
        using var scope1 = sp.CreateScope();
        using var scope2 = sp.CreateScope();

        var a1 = scope1.ServiceProvider.GetRequiredKeyedService<ILlmService>(AiProvider.Anthropic);
        var a2 = scope1.ServiceProvider.GetRequiredKeyedService<ILlmService>(AiProvider.Anthropic);
        var b  = scope2.ServiceProvider.GetRequiredKeyedService<ILlmService>(AiProvider.Anthropic);

        Assert.Same(a1, a2);      // same scope → same decorator (one enforcer/DbContext per request)
        Assert.NotSame(a1, b);    // new scope → new decorator (no captive scoped dependency)
    }
}
