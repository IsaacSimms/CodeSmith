// == System Lab LLM Service Interface == //
using CodeSmith.Core.Models;

namespace CodeSmith.Core.Interfaces;

public interface ISystemLabLlmService
{
    // Single-turn evaluation call using the accurate model
    Task<LlmResponse> EvaluateJustificationAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default);

    // Multi-turn guidance chat using the fast model
    Task<LlmResponse> GetGuidanceAsync(string systemPrompt, IReadOnlyList<ChatMessage> history, int maxTokens, CancellationToken ct = default);
}
