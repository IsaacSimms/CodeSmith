// == Infrastructure DI Registration == //
using CodeSmith.Core.Interfaces;
using CodeSmith.Infrastructure.Configuration;
using CodeSmith.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodeSmith.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for registering Infrastructure services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    // Registers all CodeSmith Infrastructure services including Anthropic API client, session store, and HTTP resilience pipeline
    public static IServiceCollection AddCodeSmithInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<AnthropicOptions>(
            configuration.GetSection(AnthropicOptions.SectionName));

        // Register session store as singleton (thread-safe ConcurrentDictionary)
        services.AddSingleton<ISessionStore, InMemorySessionStore>();

        // Register Anthropic service as scoped
        services.AddScoped<IAnthropicService, AnthropicService>();

        // Register code execution service as scoped
        services.AddScoped<ICodeExecutionService, CodeExecutionService>();

        // Register a named HttpClient with resilience for any direct HTTP needs
        services.AddHttpClient("Anthropic")
            .AddStandardResilienceHandler();

        return services;
    }
}
