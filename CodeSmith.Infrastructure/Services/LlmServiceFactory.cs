// == LLM Service Factory == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;

namespace CodeSmith.Infrastructure.Services;

/// <summary>
/// Resolves the correct <see cref="ILlmService"/> at call time based on the requested <see cref="AiProvider"/>.
/// Both implementations are registered in DI; this factory selects between them per request.
/// </summary>
public class LlmServiceFactory : ILlmServiceFactory
{
    private readonly AnthropicLlmService _anthropic;
    private readonly OpenAiLlmService    _openAi;

    public LlmServiceFactory(AnthropicLlmService anthropic, OpenAiLlmService openAi)
    {
        _anthropic = anthropic;
        _openAi    = openAi;
    }

    public ILlmService GetService(AiProvider provider) => provider switch
    {
        AiProvider.OpenAi => _openAi,
        _                 => _anthropic,
    };
}
