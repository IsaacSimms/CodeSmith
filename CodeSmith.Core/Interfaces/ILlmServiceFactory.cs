// == LLM Service Factory Interface == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Resolves the correct <see cref="ILlmService"/> implementation for a given provider at call time.
/// </summary>
public interface ILlmServiceFactory
{
    ILlmService GetService(AiProvider provider);
}
