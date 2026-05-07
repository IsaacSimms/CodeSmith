using CodeSmith.Core.Models;

namespace CodeSmith.Core.Interfaces;

public interface IPromptLabLlmService
{
    Task<LlmResponse> SimulatePromptAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default);
    Task<LlmResponse> EvaluateResponseAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default);
    Task<LlmResponse> GenerateTestInputsAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default);
}
