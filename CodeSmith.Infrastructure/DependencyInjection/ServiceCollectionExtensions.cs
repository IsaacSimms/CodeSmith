// == Infrastructure DI Registration == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Infrastructure.Configuration;
using CodeSmith.Infrastructure.Persistence;
using CodeSmith.Infrastructure.Persistence.Repositories;
using CodeSmith.Infrastructure.Services;
using CodeSmith.Infrastructure.Services.DynamicSessions;
using CodeSmith.Infrastructure.Services.Piston;
using CodeSmith.Infrastructure.Services.PromptLab;
using CodeSmith.Infrastructure.Services.SystemLab;
using CodeSmith.Infrastructure.Services.Usage;
using CodeSmith.Infrastructure.Services.Usage.Decorators;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        // Bind configuration. Provider options are validated against the pricing rate table and fail
        // fast at startup if a configured model has no rate (prevents silent model/rate-table drift).
        services.AddValidatedProviderOptions<AnthropicOptions>(configuration, AnthropicOptions.SectionName, AiProvider.Anthropic, o => o.AccurateModel, o => o.FastModel);
        services.AddValidatedProviderOptions<OpenAiOptions>(configuration, OpenAiOptions.SectionName, AiProvider.OpenAi, o => o.AccurateModel, o => o.FastModel);
        services.AddValidatedProviderOptions<XaiOptions>(configuration, XaiOptions.SectionName, AiProvider.Xai, o => o.AccurateModel, o => o.FastModel);
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.Configure<CodeExecutionOptions>(configuration.GetSection(CodeExecutionOptions.SectionName));

        // == Usage / Data Layer (SaaS cost protection) ==
        services.Configure<UsageOptions>(configuration.GetSection(UsageOptions.SectionName ?? "Usage"));
        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));
        // Pooled contexts skip per-request DbContext construction/disposal — meaningful when every
        // LLM completion opens a scope. The unpooled fallback keeps composition valid without a DB
        // (tests / dev without a connection string), where a pool cannot be configured.
        var connectionString = configuration.GetConnectionString("CodeSmithDb");
        if (!string.IsNullOrWhiteSpace(connectionString))
            services.AddDbContextPool<CodeSmithDbContext>(opts => opts.UseSqlServer(connectionString));
        else
            services.AddDbContext<CodeSmithDbContext>(_ => { });

        // Enforcement storage crosses the deep IUsageStore seam (snapshot read + one-save persist);
        // the per-entity repositories below remain for billing's read/top-up paths only.
        services.AddScoped<CodeSmith.Core.Interfaces.IUsageStore, CodeSmith.Infrastructure.Persistence.Repositories.EfUsageStore>();
        services.AddScoped<CodeSmith.Core.Interfaces.ICreditBalanceRepository, CodeSmith.Infrastructure.Persistence.Repositories.EfCreditBalanceRepository>();
        services.AddScoped<CodeSmith.Core.Interfaces.IUsageLedgerRepository, CodeSmith.Infrastructure.Persistence.Repositories.EfUsageLedgerRepository>();
        services.AddScoped<CodeSmith.Core.Interfaces.IStripeCreditStore, CodeSmith.Infrastructure.Persistence.Repositories.EfStripeCreditStore>();

        // == Billing (Stripe prepaid credits) — writes credits only; never debits ==
        services.AddSingleton<CodeSmith.Infrastructure.Billing.IStripeEventReader, CodeSmith.Infrastructure.Billing.StripeEventReader>();
        services.AddScoped<CodeSmith.Core.Interfaces.IBillingService, CodeSmith.Infrastructure.Billing.StripeBillingService>();
        services.AddSingleton<CodeSmith.Core.Interfaces.ILlmPricing, CodeSmith.Infrastructure.Services.Usage.LlmPricing>();
        services.AddScoped<CodeSmith.Core.Interfaces.IUsageEnforcer, CodeSmith.Infrastructure.Services.Usage.UsageEnforcer>();
        // Per-user lock registry — singleton so check/record serialize across requests and concurrent completions
        services.AddSingleton<CodeSmith.Core.Interfaces.IUserUsageLock, CodeSmith.Infrastructure.Services.Usage.UserUsageLock>();

        // Register session stores as singletons (thread-safe ConcurrentDictionary generic)
        services.AddSingleton<ISessionStore<CodeSmith.Core.Models.ProblemSession>, InMemorySessionStore<CodeSmith.Core.Models.ProblemSession>>();
        services.AddSingleton<IPromptLabSessionStore, InMemoryPromptLabSessionStore>();

        // == LLM Provider Registration == //
        // Two layers behind the AiProvider key:
        //   1. Raw provider adapters — stateless singletons, registered under a "raw:{provider}" key.
        //   2. The usage-enforcing decorator — scoped, keyed by the AiProvider enum, wrapping the raw
        //      adapter. Scoped is required so the decorator's IUsageEnforcer (and its DbContext) are
        //      request-scoped rather than captured for the app lifetime. The factory resolves layer 2.
        const string xaiEndpoint = "https://api.x.ai/v1"; // xAI's OpenAI-compatible base URL

        services.AddKeyedSingleton<ILlmService>(RawKey(AiProvider.Anthropic), (sp, _) =>
            new AnthropicLlmService(
                sp.GetRequiredService<IOptions<AnthropicOptions>>(),
                sp.GetRequiredService<ILogger<AnthropicLlmService>>()));

        services.AddKeyedSingleton<ILlmService>(RawKey(AiProvider.OpenAi), (sp, _) =>
        {
            var o = sp.GetRequiredService<IOptions<OpenAiOptions>>().Value;
            return new OpenAiCompatibleLlmService(
                AiProvider.OpenAi, o.ApiKey, o.AccurateModel, o.FastModel, o.ContextWindow,
                endpoint: null, sp.GetRequiredService<ILogger<OpenAiCompatibleLlmService>>(),
                timeoutSeconds: o.TimeoutSeconds);
        });

        services.AddKeyedSingleton<ILlmService>(RawKey(AiProvider.Xai), (sp, _) =>
        {
            var o = sp.GetRequiredService<IOptions<XaiOptions>>().Value;
            return new OpenAiCompatibleLlmService(
                AiProvider.Xai, o.ApiKey, o.AccurateModel, o.FastModel, o.ContextWindow,
                endpoint: xaiEndpoint, sp.GetRequiredService<ILogger<OpenAiCompatibleLlmService>>(),
                timeoutSeconds: o.TimeoutSeconds);
        });

        foreach (var provider in Enum.GetValues<AiProvider>())
        {
            var captured = provider; // avoid closure-over-loop-variable capture
            services.AddKeyedScoped<ILlmService>(captured, (sp, _) =>
                new UsageEnforcingLlmService(
                    sp.GetRequiredKeyedService<ILlmService>(RawKey(captured)),
                    sp.GetRequiredService<ICurrentUser>(),
                    sp.GetRequiredService<IUsageEnforcer>(),
                    sp.GetRequiredService<ILlmPricing>(),
                    captured));
        }

        services.AddScoped<ILlmServiceFactory, LlmServiceFactory>();

        // Shared multi-turn guidance Module — owns the append/trim/call/persist/rollback turn invariant
        // for all three surfaces. Scoped because it resolves the scoped ILlmServiceFactory.
        services.AddScoped<IGuidanceConversation, GuidanceConversation>();

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
        // "Piston" (default) → local Docker sandbox. "LocalProcess" → host processes (dev only).
        // "DynamicSessions" → Azure Container Apps custom session pool (Hyper-V sandboxes).
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
        else if (string.Equals(backend, "DynamicSessions", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient(DynamicSessionsHttpClient.Name, (sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<CodeExecutionOptions>>().Value.DynamicSessions;
                if (string.IsNullOrWhiteSpace(opts.PoolManagementEndpoint))
                    throw new InvalidOperationException(
                        "CodeExecution:DynamicSessions:PoolManagementEndpoint is required when Backend is DynamicSessions.");
                client.BaseAddress = new Uri(opts.PoolManagementEndpoint.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
            }).AddStandardResilienceHandler();

            services.AddSingleton<IDynamicSessionsTokenProvider, DefaultAzureDynamicSessionsTokenProvider>();
            services.AddScoped<ICodeExecutionService, DynamicSessionsCodeExecutionService>();
        }
        else
        {
            throw new InvalidOperationException(
                $"Unknown CodeExecution:Backend value '{backend}'. Expected 'Piston', 'LocalProcess', or 'DynamicSessions'.");
        }

        // Provide a default ICurrentUser so decorator registration succeeds.
        // The real implementation (HttpCurrentUser with header bypass + Entra) is registered in Api layer and will override.
        if (!services.Any(sd => sd.ServiceType == typeof(ICurrentUser)))
        {
            services.AddScoped<ICurrentUser, NoopCurrentUser>();
        }

        return services;
    }

    // DI key for a provider's raw (un-decorated) LLM adapter
    private static string RawKey(AiProvider provider) => $"raw:{provider}";

    // Binds a provider's options and fails fast at startup if either configured model is absent from the
    // pricing rate table, so model/rate-table drift can never silently mis-charge a live request.
    private static void AddValidatedProviderOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName,
        AiProvider provider,
        Func<TOptions, string> accurateModel,
        Func<TOptions, string> fastModel)
        where TOptions : class
    {
        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .Validate(o => LlmPricingCatalog.IsModelPriced(provider, accurateModel(o)),
                      $"{provider}: configured AccurateModel is not present in the LLM pricing rate table (LlmPricingCatalog).")
            .Validate(o => LlmPricingCatalog.IsModelPriced(provider, fastModel(o)),
                      $"{provider}: configured FastModel is not present in the LLM pricing rate table (LlmPricingCatalog).")
            .ValidateOnStart();
    }
}
