// == Infrastructure DI Registration == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Infrastructure.Configuration;
using CodeSmith.Infrastructure.Services;
using CodeSmith.Infrastructure.Services.Piston;
using CodeSmith.Infrastructure.Services.PromptLab;
using CodeSmith.Infrastructure.Services.SystemLab;
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

        // Register session stores as singletons (thread-safe ConcurrentDictionary generic)
        services.AddSingleton<ISessionStore<CodeSmith.Core.Models.ProblemSession>, InMemorySessionStore<CodeSmith.Core.Models.ProblemSession>>();
        services.AddSingleton<IPromptLabSessionStore, InMemoryPromptLabSessionStore>();

        // == LLM Provider Registration == //
        // Both implementations are registered as singletons so they can be reused.
        // Keyed services enable the factory to route by AiProvider enum at call time.
        services.AddSingleton<AnthropicLlmService>();
        services.AddSingleton<OpenAiLlmService>();

        // Register keyed tutoring services (both providers implement ITutoringLlmService)
        services.AddKeyedSingleton<ITutoringLlmService>(
            AiProvider.Anthropic,
            (sp, _) => sp.GetRequiredService<AnthropicLlmService>());
        services.AddKeyedSingleton<ITutoringLlmService>(
            AiProvider.OpenAi,
            (sp, _) => sp.GetRequiredService<OpenAiLlmService>());

        // Register keyed prompt-lab services (both providers implement IPromptLabLlmService)
        services.AddKeyedSingleton<IPromptLabLlmService>(
            AiProvider.Anthropic,
            (sp, _) => sp.GetRequiredService<AnthropicLlmService>());
        services.AddKeyedSingleton<IPromptLabLlmService>(
            AiProvider.OpenAi,
            (sp, _) => sp.GetRequiredService<OpenAiLlmService>());

        // Register keyed system-lab services (both providers implement ISystemLabLlmService)
        services.AddKeyedSingleton<ISystemLabLlmService>(
            AiProvider.Anthropic,
            (sp, _) => sp.GetRequiredService<AnthropicLlmService>());
        services.AddKeyedSingleton<ISystemLabLlmService>(
            AiProvider.OpenAi,
            (sp, _) => sp.GetRequiredService<OpenAiLlmService>());

        services.AddScoped<ILlmServiceFactory, LlmServiceFactory>();

        // Stateless singletons — safe and avoid repeated allocations
        services.AddSingleton<ITutoringPromptTemplates, TutoringPromptTemplates>();
        services.AddSingleton<IProblemResponseParser, ProblemResponseParser>();

        // ProblemGenerator is scoped because it depends on scoped ILlmServiceFactory
        services.AddScoped<IProblemGenerator, ProblemGenerator>();

        // TutoringService is session-aware and delegates to IProblemGenerator and ILlmServiceFactory
        services.AddScoped<ITutoringService, TutoringService>();

        // Register Prompt Lab services
        services.AddScoped<IPromptSimulator, PromptSimulator>();
        services.AddScoped<IPromptEvaluator, PromptEvaluator>();
        services.AddScoped<ITestInputGenerator, TestInputGenerator>();
        services.AddScoped<IPromptLabService, PromptLabService>();

        // Register System Lab services
        services.AddSingleton<ISystemLabSessionStore, InMemorySystemLabSessionStore>();
        services.AddScoped<ISystemLabEvaluator, SystemLabEvaluator>();
        services.AddScoped<ISystemLabService, SystemLabService>();

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
