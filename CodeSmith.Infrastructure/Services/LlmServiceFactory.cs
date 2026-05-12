// == LLM Service Factory == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CodeSmith.Infrastructure.Services;

/// <summary>
/// Resolves a keyed LLM service implementation for the given provider at call time.
/// Uses keyed DI to route by AiProvider enum.
/// </summary>
public class LlmServiceFactory(IServiceProvider sp) : ILlmServiceFactory
{
    public T GetLlmService<T>(AiProvider provider) where T : class
        => sp.GetRequiredKeyedService<T>(provider);
}
