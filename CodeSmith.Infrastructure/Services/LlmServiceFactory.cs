// == LLM Service Factory == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CodeSmith.Infrastructure.Services;

/// <summary>
/// Resolves the correct tutoring or prompt-lab LLM service implementation for a given provider at call time.
/// Uses keyed DI to route by AiProvider enum.
/// </summary>
public class LlmServiceFactory : ILlmServiceFactory
{
    private readonly IServiceProvider _sp;

    public LlmServiceFactory(IServiceProvider sp)
    {
        _sp = sp;
    }

    public ITutoringLlmService GetTutoringService(AiProvider provider)
        => _sp.GetRequiredKeyedService<ITutoringLlmService>(provider);

    public IPromptLabLlmService GetPromptLabService(AiProvider provider)
        => _sp.GetRequiredKeyedService<IPromptLabLlmService>(provider);
}
