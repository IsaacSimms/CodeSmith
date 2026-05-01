// == Infrastructure DI Registration == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Infrastructure.Configuration;
using CodeSmith.Infrastructure.Services;
using CodeSmith.Infrastructure.Services.Piston;
using CodeSmith.Infrastructure.Services.PromptLab;
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
        services.Configure<AnthropicOptions>(configuration.GetSection(AnthropicOptions.SectionName));
        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.Configure<CodeExecutionOptions>(configuration.GetSection(CodeExecutionOptions.SectionName));

        // Register session store as singleton (thread-safe ConcurrentDictionary)
        services.AddSingleton<ISessionStore, InMemorySessionStore>();

        // == LLM Provider Selection == //
        // Both provider implementations are registered so their options are validated at startup.
        // Only the active provider is bound to ILlmService — the other is never instantiated per-request.
        services.AddScoped<AnthropicLlmService>();
        services.AddScoped<OpenAiLlmService>();

        var activeProvider = configuration.GetSection(AiOptions.SectionName)[nameof(AiOptions.ActiveProvider)] ?? "Anthropic";

        if (Enum.TryParse<AiProvider>(activeProvider, ignoreCase: true, out var provider) && provider == AiProvider.OpenAi)
            services.AddScoped<ILlmService>(sp => sp.GetRequiredService<OpenAiLlmService>());
        else
            services.AddScoped<ILlmService>(sp => sp.GetRequiredService<AnthropicLlmService>());

        // TutoringService is session-aware and delegates completions to ILlmService
        services.AddScoped<ITutoringService, TutoringService>();

        // Register Prompt Lab services
        services.AddSingleton<IPromptLabSessionStore, InMemoryPromptLabSessionStore>();
        services.AddScoped<IPromptLabService, PromptLabService>();

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
