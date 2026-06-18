// == Infrastructure DI Registration == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Infrastructure.Configuration;
using CodeSmith.Infrastructure.Persistence;
using CodeSmith.Infrastructure.Persistence.Repositories;
using CodeSmith.Infrastructure.Services;
using CodeSmith.Infrastructure.Services.Piston;
using CodeSmith.Infrastructure.Services.PromptLab;
using CodeSmith.Infrastructure.Services.SystemLab;
using CodeSmith.Infrastructure.Services.Usage;
using CodeSmith.Infrastructure.Services.Usage.Decorators;
using Microsoft.EntityFrameworkCore;
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
        services.Configure<XaiOptions>(configuration.GetSection(XaiOptions.SectionName));
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.Configure<CodeExecutionOptions>(configuration.GetSection(CodeExecutionOptions.SectionName));

        // == Usage / Data Layer (SaaS cost protection) ==
        services.Configure<UsageOptions>(configuration.GetSection(UsageOptions.SectionName ?? "Usage"));
        services.AddDbContext<CodeSmithDbContext>(opts =>
        {
            var conn = configuration.GetConnectionString("CodeSmithDb");
            if (!string.IsNullOrWhiteSpace(conn))
                opts.UseSqlServer(conn);
        });

        services.AddScoped<CodeSmith.Core.Interfaces.ICreditBalanceRepository, CodeSmith.Infrastructure.Persistence.Repositories.EfCreditBalanceRepository>();
        services.AddScoped<CodeSmith.Core.Interfaces.IUsageLedgerRepository, CodeSmith.Infrastructure.Persistence.Repositories.EfUsageLedgerRepository>();
        services.AddSingleton<CodeSmith.Core.Interfaces.ILlmPricing, CodeSmith.Infrastructure.Services.Usage.LlmPricing>();
        services.AddScoped<CodeSmith.Core.Interfaces.IUsageEnforcer, CodeSmith.Infrastructure.Services.Usage.UsageEnforcer>();

        // Register session stores as singletons (thread-safe ConcurrentDictionary generic)
        services.AddSingleton<ISessionStore<CodeSmith.Core.Models.ProblemSession>, InMemorySessionStore<CodeSmith.Core.Models.ProblemSession>>();
        services.AddSingleton<IPromptLabSessionStore, InMemoryPromptLabSessionStore>();

        // == LLM Provider Registration == //
        // All implementations are registered as singletons so they can be reused.
        // Keyed services enable the factory to route by AiProvider enum at call time.
        services.AddSingleton<AnthropicLlmService>();
        services.AddSingleton<OpenAiLlmService>();
        services.AddSingleton<XaiLlmService>();

        // Register keyed LLM services through usage-enforcing decorators (seam in front of raw adapters)
        // The decorator receives the provider at resolution time and wraps check/record around every call.
        services.AddKeyedSingleton<ITutoringLlmService>(
            AiProvider.Anthropic,
            (sp, _) => new UsageEnforcingTutoringLlmService(
                sp.GetRequiredService<AnthropicLlmService>(),
                sp.GetRequiredService<ICurrentUser>(),
                sp.GetRequiredService<IUsageEnforcer>(),
                sp.GetRequiredService<ILlmPricing>(),
                AiProvider.Anthropic));

        services.AddKeyedSingleton<ITutoringLlmService>(
            AiProvider.OpenAi,
            (sp, _) => new UsageEnforcingTutoringLlmService(
                sp.GetRequiredService<OpenAiLlmService>(),
                sp.GetRequiredService<ICurrentUser>(),
                sp.GetRequiredService<IUsageEnforcer>(),
                sp.GetRequiredService<ILlmPricing>(),
                AiProvider.OpenAi));

        services.AddKeyedSingleton<IPromptLabLlmService>(
            AiProvider.Anthropic,
            (sp, _) => new UsageEnforcingPromptLabLlmService(
                sp.GetRequiredService<AnthropicLlmService>(),
                sp.GetRequiredService<ICurrentUser>(),
                sp.GetRequiredService<IUsageEnforcer>(),
                sp.GetRequiredService<ILlmPricing>(),
                AiProvider.Anthropic));

        services.AddKeyedSingleton<IPromptLabLlmService>(
            AiProvider.OpenAi,
            (sp, _) => new UsageEnforcingPromptLabLlmService(
                sp.GetRequiredService<OpenAiLlmService>(),
                sp.GetRequiredService<ICurrentUser>(),
                sp.GetRequiredService<IUsageEnforcer>(),
                sp.GetRequiredService<ILlmPricing>(),
                AiProvider.OpenAi));

        services.AddKeyedSingleton<ISystemLabLlmService>(
            AiProvider.Anthropic,
            (sp, _) => new UsageEnforcingSystemLabLlmService(
                sp.GetRequiredService<AnthropicLlmService>(),
                sp.GetRequiredService<ICurrentUser>(),
                sp.GetRequiredService<IUsageEnforcer>(),
                sp.GetRequiredService<ILlmPricing>(),
                AiProvider.Anthropic));

        services.AddKeyedSingleton<ISystemLabLlmService>(
            AiProvider.OpenAi,
            (sp, _) => new UsageEnforcingSystemLabLlmService(
                sp.GetRequiredService<OpenAiLlmService>(),
                sp.GetRequiredService<ICurrentUser>(),
                sp.GetRequiredService<IUsageEnforcer>(),
                sp.GetRequiredService<ILlmPricing>(),
                AiProvider.OpenAi));

        // Xai
        services.AddKeyedSingleton<ITutoringLlmService>(
            AiProvider.Xai,
            (sp, _) => new UsageEnforcingTutoringLlmService(
                sp.GetRequiredService<XaiLlmService>(),
                sp.GetRequiredService<ICurrentUser>(),
                sp.GetRequiredService<IUsageEnforcer>(),
                sp.GetRequiredService<ILlmPricing>(),
                AiProvider.Xai));

        services.AddKeyedSingleton<IPromptLabLlmService>(
            AiProvider.Xai,
            (sp, _) => new UsageEnforcingPromptLabLlmService(
                sp.GetRequiredService<XaiLlmService>(),
                sp.GetRequiredService<ICurrentUser>(),
                sp.GetRequiredService<IUsageEnforcer>(),
                sp.GetRequiredService<ILlmPricing>(),
                AiProvider.Xai));

        services.AddKeyedSingleton<ISystemLabLlmService>(
            AiProvider.Xai,
            (sp, _) => new UsageEnforcingSystemLabLlmService(
                sp.GetRequiredService<XaiLlmService>(),
                sp.GetRequiredService<ICurrentUser>(),
                sp.GetRequiredService<IUsageEnforcer>(),
                sp.GetRequiredService<ILlmPricing>(),
                AiProvider.Xai));

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

        // Provide a default ICurrentUser so decorator registration succeeds.
        // The real implementation (HttpCurrentUser with header bypass + Entra) is registered in Api layer and will override.
        if (!services.Any(sd => sd.ServiceType == typeof(ICurrentUser)))
        {
            services.AddScoped<ICurrentUser, NoopCurrentUser>();
        }

        return services;
    }
}
