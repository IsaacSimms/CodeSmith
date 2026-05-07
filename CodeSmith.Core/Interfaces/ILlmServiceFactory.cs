// == LLM Service Factory Interface == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Resolves the correct tutoring or prompt-lab LLM service implementation for a given provider at call time.
/// </summary>
public interface ILlmServiceFactory
{
    ITutoringLlmService GetTutoringService(AiProvider provider);
    IPromptLabLlmService GetPromptLabService(AiProvider provider);
}
