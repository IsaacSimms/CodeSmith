// == LLM Service Interface == //
using CodeSmith.Core.Models;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Stateless provider abstraction for LLM completions.
/// Each implementation maps named capability methods to provider-specific models internally.
/// Callers express intent (simulate, evaluate, guide) — not model tier or provider name.
/// </summary>
public interface ILlmService
{
    // Generates a raw problem response given a fully-built system prompt and user message
    Task<LlmResponse> GenerateProblemAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default);

    // Returns guidance given a system prompt and full conversation history
    Task<LlmResponse> GetGuidanceAsync(string systemPrompt, IReadOnlyList<ChatMessage> history, int maxTokens, CancellationToken ct = default);

    // Simulates how a model responds to a prompt engineering attempt for one test input
    Task<LlmResponse> SimulatePromptAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default);

    // Evaluates a simulation output against a rubric, returning structured JSON feedback
    Task<LlmResponse> EvaluateResponseAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default);

    // Generates dynamic test inputs for a prompt lab challenge
    Task<LlmResponse> GenerateTestInputsAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default);
}
