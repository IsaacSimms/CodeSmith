// == OpenAI LLM Service Implementation == //
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace CodeSmith.Infrastructure.Services;

/// <summary>
/// Implementation of <see cref="ILlmService"/> using the official OpenAI .NET SDK.
/// Stateless: no session management. Maps named capability methods to GPT models internally.
/// </summary>
public class OpenAiLlmService : ILlmService
{
    private readonly OpenAIClient _client;
    private readonly ILogger<OpenAiLlmService> _logger;

    private const string AccurateModel = "gpt-4.1";       // Used for generation, evaluation, test input creation
    private const string FastModel     = "gpt-4.1-mini";  // Used for guidance and simulation — fast and cheap
    private const int    ContextWindow = 1_047_576;        // Token limit for GPT-4.1 / GPT-4.1-mini

    public OpenAiLlmService(IOptions<OpenAiOptions> options, ILogger<OpenAiLlmService> logger)
    {
        _client = new OpenAIClient(options.Value.ApiKey);
        _logger = logger;
    }

    // == Problem Generation (accurate model) == //

    public async Task<LlmResponse> GenerateProblemAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
    {
        try
        {
            var chatClient = _client.GetChatClient(AccurateModel);
            var response   = await chatClient.CompleteChatAsync(
                [new SystemChatMessage(systemPrompt), new UserChatMessage(userMessage)],
                new ChatCompletionOptions { MaxOutputTokenCount = maxTokens },
                ct);

            _logger.LogDebug("GenerateProblemAsync: {InputTokens} input tokens", response.Value.Usage.InputTokenCount);

            return new LlmResponse
            {
                Content           = response.Value.Content[0].Text,
                InputTokensUsed   = response.Value.Usage.InputTokenCount,
                ContextWindowSize = ContextWindow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI GenerateProblemAsync failed");
            throw new AiServiceException("OpenAI problem generation failed. Please try again.", ex);
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

            var chatClient = _client.GetChatClient(FastModel);
            var response   = await chatClient.CompleteChatAsync(
                messages,
                new ChatCompletionOptions { MaxOutputTokenCount = maxTokens },
                ct);

            return new LlmResponse
            {
                Content           = response.Value.Content[0].Text,
                InputTokensUsed   = response.Value.Usage.InputTokenCount,
                ContextWindowSize = ContextWindow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI GetGuidanceAsync failed");
            throw new AiServiceException("OpenAI guidance failed. Please try again.", ex);
        }
    }

    // == Simulation (fast model, single turn) == //

    public async Task<LlmResponse> SimulatePromptAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
    {
        try
        {
            var chatClient = _client.GetChatClient(FastModel);
            var response   = await chatClient.CompleteChatAsync(
                [new SystemChatMessage(systemPrompt), new UserChatMessage(userMessage)],
                new ChatCompletionOptions { MaxOutputTokenCount = maxTokens },
                ct);

            return new LlmResponse
            {
                Content           = response.Value.Content[0].Text,
                InputTokensUsed   = response.Value.Usage.InputTokenCount,
                ContextWindowSize = ContextWindow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI SimulatePromptAsync failed");
            throw new AiServiceException("OpenAI simulation failed. Please try again.", ex);
        }
    }

    // == Evaluation (accurate model, single turn) == //

    public async Task<LlmResponse> EvaluateResponseAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
    {
        try
        {
            var chatClient = _client.GetChatClient(AccurateModel);
            var response   = await chatClient.CompleteChatAsync(
                [new SystemChatMessage(systemPrompt), new UserChatMessage(userMessage)],
                new ChatCompletionOptions { MaxOutputTokenCount = maxTokens },
                ct);

            return new LlmResponse
            {
                Content           = response.Value.Content[0].Text,
                InputTokensUsed   = response.Value.Usage.InputTokenCount,
                ContextWindowSize = ContextWindow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI EvaluateResponseAsync failed");
            throw new AiServiceException("OpenAI evaluation failed. Please try again.", ex);
        }
    }

    // == Test Input Generation (accurate model, single turn) == //

    public async Task<LlmResponse> GenerateTestInputsAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
    {
        try
        {
            var chatClient = _client.GetChatClient(AccurateModel);
            var response   = await chatClient.CompleteChatAsync(
                [new SystemChatMessage(systemPrompt), new UserChatMessage(userMessage)],
                new ChatCompletionOptions { MaxOutputTokenCount = maxTokens },
                ct);

            return new LlmResponse
            {
                Content           = response.Value.Content[0].Text,
                InputTokensUsed   = response.Value.Usage.InputTokenCount,
                ContextWindowSize = ContextWindow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI GenerateTestInputsAsync failed");
            throw new AiServiceException("OpenAI test input generation failed. Please try again.", ex);
        }
    }
}
