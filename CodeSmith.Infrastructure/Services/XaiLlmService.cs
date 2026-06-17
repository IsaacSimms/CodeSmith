// == Xai LLM Service Implementation == //
using System;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace CodeSmith.Infrastructure.Services;

/// <summary>
/// Implementation of <see cref="ITutoringLlmService"/>, <see cref="IPromptLabLlmService"/>, and <see cref="ISystemLabLlmService"/>
/// using the official OpenAI .NET SDK against the xAI (Grok) OpenAI-compatible endpoint.
/// Stateless: no session management. Maps named capability methods to Grok models internally.
/// </summary>
public class XaiLlmService : ITutoringLlmService, IPromptLabLlmService, ISystemLabLlmService
{
    private readonly OpenAIClient _client;
    private readonly XaiOptions _options;
    private readonly ILogger<XaiLlmService> _logger;

    public XaiLlmService(IOptions<XaiOptions> options, ILogger<XaiLlmService> logger)
    {
        _options = options.Value;
        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri("https://api.x.ai/v1")
        };
        _client = new OpenAIClient(new ApiKeyCredential(_options.ApiKey), clientOptions);
        _logger = logger;
    }

    // == Problem Generation (accurate model) == //

    public async Task<LlmResponse> GenerateProblemAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
    {
        try
        {
            var chatClient = _client.GetChatClient(_options.AccurateModel);
            var response   = await chatClient.CompleteChatAsync(
                [new SystemChatMessage(systemPrompt), new UserChatMessage(userMessage)],
                new ChatCompletionOptions { MaxOutputTokenCount = maxTokens },
                ct);

            _logger.LogDebug("GenerateProblemAsync: {InputTokens} input tokens", response.Value.Usage.InputTokenCount);

            return new LlmResponse
            {
                Content           = ExtractTextContent(response.Value),
                InputTokensUsed   = response.Value.Usage.InputTokenCount,
                ContextWindowSize = _options.ContextWindow,
                WasTruncated      = response.Value.FinishReason == ChatFinishReason.Length
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "xAI GenerateProblemAsync failed");
            throw new AiServiceException("xAI problem generation failed. Please try again.", ex);
        }
    }

    // == Guidance (fast model, multi-turn history) == //

    public async Task<LlmResponse> GetGuidanceAsync(string systemPrompt, IReadOnlyList<Core.Models.ChatMessage> history, int maxTokens, CancellationToken ct = default)
    {
        try
        {
            var messages = new List<OpenAI.Chat.ChatMessage> { new SystemChatMessage(systemPrompt) };

            foreach (var msg in history)
            {
                messages.Add(msg.Role == Core.Enums.MessageRole.User
                    ? new UserChatMessage(msg.Content)
                    : (OpenAI.Chat.ChatMessage)new AssistantChatMessage(msg.Content));
            }

            var chatClient = _client.GetChatClient(_options.FastModel);
            var response   = await chatClient.CompleteChatAsync(
                messages,
                new ChatCompletionOptions { MaxOutputTokenCount = maxTokens },
                ct);

            return new LlmResponse
            {
                Content           = ExtractTextContent(response.Value),
                InputTokensUsed   = response.Value.Usage.InputTokenCount,
                ContextWindowSize = _options.ContextWindow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "xAI GetGuidanceAsync failed");
            throw new AiServiceException("xAI guidance failed. Please try again.", ex);
        }
    }

    // == Simulation (fast model, single turn) == //

    public async Task<LlmResponse> SimulatePromptAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
    {
        try
        {
            var chatClient = _client.GetChatClient(_options.FastModel);
            var response   = await chatClient.CompleteChatAsync(
                [new SystemChatMessage(systemPrompt), new UserChatMessage(userMessage)],
                new ChatCompletionOptions { MaxOutputTokenCount = maxTokens },
                ct);

            return new LlmResponse
            {
                Content           = ExtractTextContent(response.Value),
                InputTokensUsed   = response.Value.Usage.InputTokenCount,
                ContextWindowSize = _options.ContextWindow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "xAI SimulatePromptAsync failed");
            throw new AiServiceException("xAI simulation failed. Please try again.", ex);
        }
    }

    // == Evaluation (accurate model, single turn) == //

    public async Task<LlmResponse> EvaluateResponseAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
    {
        try
        {
            var chatClient = _client.GetChatClient(_options.AccurateModel);
            var response   = await chatClient.CompleteChatAsync(
                [new SystemChatMessage(systemPrompt), new UserChatMessage(userMessage)],
                new ChatCompletionOptions { MaxOutputTokenCount = maxTokens },
                ct);

            return new LlmResponse
            {
                Content           = ExtractTextContent(response.Value),
                InputTokensUsed   = response.Value.Usage.InputTokenCount,
                ContextWindowSize = _options.ContextWindow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "xAI EvaluateResponseAsync failed");
            throw new AiServiceException("xAI evaluation failed. Please try again.", ex);
        }
    }

    // == System Lab: Evaluate Justification (accurate model, single turn) == //

    public async Task<LlmResponse> EvaluateJustificationAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
    {
        try
        {
            var chatClient = _client.GetChatClient(_options.AccurateModel);
            var response   = await chatClient.CompleteChatAsync(
                [new SystemChatMessage(systemPrompt), new UserChatMessage(userMessage)],
                new ChatCompletionOptions { MaxOutputTokenCount = maxTokens },
                ct);

            return new LlmResponse
            {
                Content           = ExtractTextContent(response.Value),
                InputTokensUsed   = response.Value.Usage.InputTokenCount,
                ContextWindowSize = _options.ContextWindow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "xAI EvaluateJustificationAsync failed");
            throw new AiServiceException("xAI justification evaluation failed. Please try again.", ex);
        }
    }

    // == Test Input Generation (accurate model, single turn) == //

    public async Task<LlmResponse> GenerateTestInputsAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
    {
        try
        {
            var chatClient = _client.GetChatClient(_options.AccurateModel);
            var response   = await chatClient.CompleteChatAsync(
                [new SystemChatMessage(systemPrompt), new UserChatMessage(userMessage)],
                new ChatCompletionOptions { MaxOutputTokenCount = maxTokens },
                ct);

            return new LlmResponse
            {
                Content           = ExtractTextContent(response.Value),
                InputTokensUsed   = response.Value.Usage.InputTokenCount,
                ContextWindowSize = _options.ContextWindow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "xAI GenerateTestInputsAsync failed");
            throw new AiServiceException("xAI test input generation failed. Please try again.", ex);
        }
    }

    // == Helpers == //

    private static string ExtractTextContent(ChatCompletion response)
        => response.Content.Count > 0 ? response.Content[0].Text : string.Empty;
}
