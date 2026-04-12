// == Infrastructure DI Registration == //
using CodeSmith.Core.Interfaces;
using CodeSmith.Infrastructure.Configuration;
using CodeSmith.Infrastructure.Services;
using CodeSmith.Infrastructure.Services.Piston;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CodeSmith.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for registering Infrastructure services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    // Registers all CodeSmith Infrastructure services including Anthropic API client, session store, and code execution backend
    public static IServiceCollection AddCodeSmithInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<AnthropicOptions>(
            configuration.GetSection(AnthropicOptions.SectionName));
        services.Configure<CodeExecutionOptions>(
            configuration.GetSection(CodeExecutionOptions.SectionName));

        // Register session store as singleton (thread-safe ConcurrentDictionary)
        services.AddSingleton<ISessionStore, InMemorySessionStore>();

        // Register Anthropic service as scoped
        services.AddScoped<IAnthropicService, AnthropicService>();

        // == Code Execution Backend Selection == //
        // Reads CodeExecution:Backend from config and wires the matching implementation.
        // "Piston" (default) routes to the sandboxed Docker-hosted runner.
        // "LocalProcess" runs code directly on the host — development fallback only.
        var backend = configuration.GetSection(CodeExecutionOptions.SectionName)["Backend"] ?? "Piston";

        if (string.Equals(backend, "Piston", StringComparison.OrdinalIgnoreCase))
        {
            // Named HttpClient shared by the resolver and the executor. Configured from
            // PistonOptions so dev/prod can point at different hosts without code changes.
            services.AddHttpClient(PistonHttpClient.Name, (sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<CodeExecutionOptions>>().Value.Piston;
                client.BaseAddress = new Uri(opts.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
            }).AddStandardResilienceHandler();

            services.AddSingleton<IPistonRuntimeResolver, PistonRuntimeResolver>();
            services.AddScoped<ICodeExecutionService, PistonCodeExecutionService>();
        }
        else if (string.Equals(backend, "LocalProcess", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<ICodeExecutionService, LocalProcessCodeExecutionService>();
        }
        else
        {
            throw new InvalidOperationException(
                $"Unknown CodeExecution:Backend value '{backend}'. Expected 'Piston' or 'LocalProcess'.");
        }

        // Register a named HttpClient with resilience for any direct HTTP needs
        services.AddHttpClient("Anthropic")
            .AddStandardResilienceHandler();

        return services;
    }
}
