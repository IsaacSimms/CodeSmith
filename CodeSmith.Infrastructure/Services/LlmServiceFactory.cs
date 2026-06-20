// == LLM Service Factory == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CodeSmith.Infrastructure.Services;

/// <summary>
/// Resolves the usage-enforced <see cref="ILlmService"/> keyed by <see cref="AiProvider"/> at call time.
/// Registered scoped, so the resolved (scoped) decorator gets a request-scoped usage enforcer + DbContext.
/// </summary>
public class LlmServiceFactory(IServiceProvider sp) : ILlmServiceFactory
{
    public ILlmService Get(AiProvider provider)
        => sp.GetRequiredKeyedService<ILlmService>(provider);
}
