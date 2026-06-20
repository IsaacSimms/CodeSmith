// == LLM Service Factory Interface == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Resolves the usage-enforced <see cref="ILlmService"/> for a given provider at call time.
/// Provider is a runtime value (it lives on the session), so routing goes through this factory
/// rather than constructor-time keyed injection.
/// </summary>
public interface ILlmServiceFactory
{
    ILlmService Get(AiProvider provider);
}
