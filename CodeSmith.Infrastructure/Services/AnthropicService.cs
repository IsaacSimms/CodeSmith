// == Anthropic LLM Service Implementation == //
using Anthropic;
using Anthropic.Models.Messages;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeSmith.Infrastructure.Services;

/// <summary>
/// Implementation of <see cref="ITutoringLlmService"/> and <see cref="IPromptLabLlmService"/> using the official Anthropic C# SDK.
/// Stateless: no session management. Maps named capability methods to Claude models internally.
/// </summary>
public class AnthropicLlmService : ITutoringLlmService, IPromptLabLlmService
{
    private readonly AnthropicClient _client;
    private readonly AnthropicOptions _options;
    private readonly ILogger<AnthropicLlmService> _logger;

    public AnthropicLlmService(
        IOptions<AnthropicOptions> options,
        ILogger<AnthropicLlmService> logger)
    {
        _options = options.Value;
        _client  = new AnthropicClient { ApiKey = _options.ApiKey };
        _logger  = logger;
    }

    // == Problem Generation (Sonnet, with truncation retry) == //

    public Task<LlmResponse> GenerateProblemAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
        => GenerateWithRetryAsync(systemPrompt, userMessage, maxTokens, retryCount: 0, ct);

    private async Task<LlmResponse> GenerateWithRetryAsync(string systemPrompt, string userMessage, int maxTokens, int retryCount, CancellationToken ct)
    {
        if (retryCount > 0)
            _logger.LogInformation("Retrying problem generation (attempt {Attempt}/{Max})", retryCount + 1, _options.MaxRetries + 1);

        try
        {
            var retryMessage = retryCount > 0
                ? $"{userMessage} Note: A previous attempt was cut off due to token limits. Please generate a complete problem."
                : userMessage;

            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model     = _options.AccurateModel,
                MaxTokens = maxTokens,
                System    = systemPrompt,
                Messages  = [new() { Role = Role.User, Content = retryMessage }]
            }, ct);

            // Detect truncation — Anthropic signals this via StopReason == "max_tokens"
            if (response.StopReason == "max_tokens")
            {
                _logger.LogWarning("Problem generation hit max_tokens on attempt {Attempt}/{Max}", retryCount + 1, _options.MaxRetries + 1);

                if (retryCount < _options.MaxRetries)
                    return await GenerateWithRetryAsync(systemPrompt, userMessage, maxTokens, retryCount + 1, ct);

                _logger.LogError("Problem generation failed after {Max} retry attempts due to token limit", _options.MaxRetries);
                throw new AiServiceException(
                    "Failed to generate a complete coding problem after multiple attempts. The problem was too large to generate. Please try again.");
            }

            return new LlmResponse
            {
                Content           = ExtractTextContent(response),
                InputTokensUsed   = (int)response.Usage.InputTokens,
                ContextWindowSize = _options.ContextWindow
            };
        }
        catch (Exception ex) when (ex is not AiServiceException)
        {
            _logger.LogError(ex, "Anthropic API call failed during problem generation");
            throw new AiServiceException("Failed to generate coding problem. Please try again.", ex);
        }
    }

    // == Guidance (Haiku, multi-turn history) == //

    public async Task<LlmResponse> GetGuidanceAsync(string systemPrompt, IReadOnlyList<ChatMessage> history, int maxTokens, CancellationToken ct = default)
    {
        try
        {
            var messages = history.Select(m => new MessageParam
            {
                Role    = m.Role == Core.Enums.MessageRole.User ? Role.User : Role.Assistant,
                Content = m.Content
            }).ToList();

            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model     = _options.FastModel,
                MaxTokens = maxTokens,
                System    = systemPrompt,
                Messages  = messages
            }, ct);

            return new LlmResponse
            {
                Content           = ExtractTextContent(response),
                InputTokensUsed   = (int)response.Usage.InputTokens,
                ContextWindowSize = _options.ContextWindow
            };
        }
        catch (Exception ex) when (ex is not AiServiceException)
        {
            _logger.LogError(ex, "Anthropic API call failed during guidance");
            throw new AiServiceException("Failed to get guidance. Please try again.", ex);
        }
    }

    // == Prompt Lab: Simulate (Haiku) == //

    public Task<LlmResponse> SimulatePromptAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
        => CreateSingleTurnAsync(_options.FastModel, systemPrompt, userMessage, maxTokens, "prompt simulation", ct);

    // == Prompt Lab: Evaluate (Sonnet) == //

    public Task<LlmResponse> EvaluateResponseAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
        => CreateSingleTurnAsync(_options.AccurateModel, systemPrompt, userMessage, maxTokens, "response evaluation", ct);

    // == Prompt Lab: Generate Test Inputs (Sonnet) == //

    public Task<LlmResponse> GenerateTestInputsAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
        => CreateSingleTurnAsync(_options.AccurateModel, systemPrompt, userMessage, maxTokens, "test input generation", ct);

    // == Prompt Lab Single-Turn Helper == //

    private async Task<LlmResponse> CreateSingleTurnAsync(string model, string systemPrompt, string userMessage, int maxTokens, string operationName, CancellationToken ct)
    {
        try
        {
            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model     = model,
                MaxTokens = maxTokens,
                System    = systemPrompt,
                Messages  = [new() { Role = Role.User, Content = userMessage }]
            }, ct);

            return new LlmResponse
            {
                Content           = ExtractTextContent(response),
                InputTokensUsed   = (int)response.Usage.InputTokens,
                ContextWindowSize = _options.ContextWindow
            };
        }
        catch (Exception ex) when (ex is not AiServiceException)
        {
            _logger.LogError(ex, "Anthropic API call failed during {OperationName}", operationName);
            throw new AiServiceException($"Failed during {operationName}. Please try again.", ex);
        }
    }

    // == Helpers == //

    internal static string ExtractTextContent(Message response)  // Extracts concatenated text from all content blocks in an Anthropic response
    {
        var texts = new List<string>();
        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var textBlock))
                texts.Add(textBlock.Text);
        }
        return string.Join("", texts);
    }
}
