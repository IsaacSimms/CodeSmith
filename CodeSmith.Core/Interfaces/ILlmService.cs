// == LLM Service Interface == //
using CodeSmith.Core.Models;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// The single, provider-agnostic LLM seam. One operation — a completion over a message list at a
/// chosen model tier — replaces the former capability-named methods (GenerateProblem, GetGuidance,
/// SimulatePrompt, EvaluateResponse, GenerateTestInputs, EvaluateJustification). Caller intent
/// (tier, feature) travels on the request. Implementations are stateless and map the tier to a
/// concrete model internally.
/// </summary>
public interface ILlmService
{
    Task<LlmResponse> CompleteAsync(CompletionRequest request, CancellationToken ct = default);
}
