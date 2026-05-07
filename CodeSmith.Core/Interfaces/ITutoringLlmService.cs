using CodeSmith.Core.Models;

namespace CodeSmith.Core.Interfaces;

public interface ITutoringLlmService
{
    Task<LlmResponse> GenerateProblemAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default);
    Task<LlmResponse> GetGuidanceAsync(string systemPrompt, IReadOnlyList<ChatMessage> history, int maxTokens, CancellationToken ct = default);
}
