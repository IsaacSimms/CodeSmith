// == LLM Service Factory Interface == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Resolves the correct LLM service implementation for a given provider at call time.
/// </summary>
public interface ILlmServiceFactory
{
    T GetLlmService<T>(AiProvider provider) where T : class;
}
