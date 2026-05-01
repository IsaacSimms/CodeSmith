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
/// Implementation of <see cref="ILlmService"/> using the official Anthropic C# SDK.
/// Stateless: no session management. Maps named capability methods to Claude models internally.
/// </summary>
public class AnthropicLlmService : ILlmService
{
    private readonly AnthropicClient _client;
    private readonly ILogger<AnthropicLlmService> _logger;

    private const string AccurateModel = "claude-sonnet-4-6";         // Used for generation, evaluation, test input creation
    private const string FastModel     = "claude-haiku-4-5-20251001"; // Used for guidance and simulation — fast and cheap
    private const int    ContextWindow = 200_000;                     // Token limit shared by all Claude models used here
    private const int    MaxRetries    = 2;

    public AnthropicLlmService(
        IOptions<AnthropicOptions> options,
        ILogger<AnthropicLlmService> logger)
    {
        _client = new AnthropicClient { ApiKey = options.Value.ApiKey };
        _logger = logger;
    }

    // == Problem Generation (Sonnet, with truncation retry) == //

    public Task<LlmResponse> GenerateProblemAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
        => GenerateWithRetryAsync(systemPrompt, userMessage, maxTokens, retryCount: 0, ct);

    private async Task<LlmResponse> GenerateWithRetryAsync(string systemPrompt, string userMessage, int maxTokens, int retryCount, CancellationToken ct)
    {
        if (retryCount > 0)
            _logger.LogInformation("Retrying problem generation (attempt {Attempt}/{Max})", retryCount + 1, MaxRetries + 1);

        try
        {
            var retryMessage = retryCount > 0
                ? $"{userMessage} Note: A previous attempt was cut off due to token limits. Please generate a complete problem."
                : userMessage;

            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model     = AccurateModel,
                MaxTokens = maxTokens,
                System    = systemPrompt,
                Messages  = [new() { Role = Role.User, Content = retryMessage }]
            }, ct);

            // Detect truncation — Anthropic signals this via StopReason == "max_tokens"
            if (response.StopReason == "max_tokens")
            {
                _logger.LogWarning("Problem generation hit max_tokens on attempt {Attempt}/{Max}", retryCount + 1, MaxRetries + 1);

                if (retryCount < MaxRetries)
                    return await GenerateWithRetryAsync(systemPrompt, userMessage, maxTokens, retryCount + 1, ct);

                _logger.LogError("Problem generation failed after {Max} retry attempts due to token limit", MaxRetries);
                throw new AiServiceException(
                    "Failed to generate a complete coding problem after multiple attempts. The problem was too large to generate. Please try again.");
            }

            return new LlmResponse
            {
                Content           = ExtractTextContent(response),
                InputTokensUsed   = (int)response.Usage.InputTokens,
                ContextWindowSize = ContextWindow
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
                Model     = FastModel,
                MaxTokens = maxTokens,
                System    = systemPrompt,
                Messages  = messages
            }, ct);

            return new LlmResponse
            {
                Content           = ExtractTextContent(response),
                InputTokensUsed   = (int)response.Usage.InputTokens,
                ContextWindowSize = ContextWindow
            };
        }
        catch (Exception ex) when (ex is not AiServiceException)
        {
            _logger.LogError(ex, "Anthropic API call failed during guidance");
            throw new AiServiceException("Failed to get guidance. Please try again.", ex);
        }
    }

    // == Prompt Lab: Simulate (Haiku) == //

    public async Task<LlmResponse> SimulatePromptAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model     = FastModel,
                MaxTokens = maxTokens,
                System    = systemPrompt,
                Messages  = [new() { Role = Role.User, Content = userMessage }]
            }, ct);

            return new LlmResponse
            {
                Content           = ExtractTextContent(response),
                InputTokensUsed   = (int)response.Usage.InputTokens,
                ContextWindowSize = ContextWindow
            };
        }
        catch (Exception ex) when (ex is not AiServiceException)
        {
            _logger.LogError(ex, "Anthropic API call failed during prompt simulation");
            throw new AiServiceException("Failed to simulate prompt. Please try again.", ex);
        }
    }

    // == Prompt Lab: Evaluate (Sonnet) == //

    public async Task<LlmResponse> EvaluateResponseAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model     = AccurateModel,
                MaxTokens = maxTokens,
                System    = systemPrompt,
                Messages  = [new() { Role = Role.User, Content = userMessage }]
            }, ct);

            return new LlmResponse
            {
                Content           = ExtractTextContent(response),
                InputTokensUsed   = (int)response.Usage.InputTokens,
                ContextWindowSize = ContextWindow
            };
        }
        catch (Exception ex) when (ex is not AiServiceException)
        {
            _logger.LogError(ex, "Anthropic API call failed during response evaluation");
            throw new AiServiceException("Failed to evaluate response. Please try again.", ex);
        }
    }

    // == Prompt Lab: Generate Test Inputs (Sonnet) == //

    public async Task<LlmResponse> GenerateTestInputsAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model     = AccurateModel,
                MaxTokens = maxTokens,
                System    = systemPrompt,
                Messages  = [new() { Role = Role.User, Content = userMessage }]
            }, ct);

            return new LlmResponse
            {
                Content           = ExtractTextContent(response),
                InputTokensUsed   = (int)response.Usage.InputTokens,
                ContextWindowSize = ContextWindow
            };
        }
        catch (Exception ex) when (ex is not AiServiceException)
        {
            _logger.LogError(ex, "Anthropic API call failed during test input generation");
            throw new AiServiceException("Failed to generate test inputs. Please try again.", ex);
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
