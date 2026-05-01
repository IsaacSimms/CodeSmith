// == OpenAI LLM Service Stub == //
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CodeSmith.Infrastructure.Services;

/// <summary>
/// Stub implementation of <see cref="ILlmService"/> for OpenAI.
/// Architecture is wired and testable; full SDK integration is not yet implemented.
/// </summary>
public class OpenAiLlmService : ILlmService
{
    public OpenAiLlmService(IOptions<OpenAiOptions> options) { }

    public Task<LlmResponse> GenerateProblemAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
        => throw new NotImplementedException("OpenAI provider is not yet implemented.");

    public Task<LlmResponse> GetGuidanceAsync(string systemPrompt, IReadOnlyList<ChatMessage> history, int maxTokens, CancellationToken ct = default)
        => throw new NotImplementedException("OpenAI provider is not yet implemented.");

    public Task<LlmResponse> SimulatePromptAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
        => throw new NotImplementedException("OpenAI provider is not yet implemented.");

    public Task<LlmResponse> EvaluateResponseAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
        => throw new NotImplementedException("OpenAI provider is not yet implemented.");

    public Task<LlmResponse> GenerateTestInputsAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
        => throw new NotImplementedException("OpenAI provider is not yet implemented.");
}
