// == Anthropic LLM Service Implementation == //
using Anthropic;
using Anthropic.Models.Messages;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeSmith.Infrastructure.Services;

/// <summary>
/// Implementation of <see cref="ILlmService"/> using the official Anthropic C# SDK.
/// Stateless: no session management. Maps the requested <see cref="ModelTier"/> to a Claude model internally.
/// </summary>
public class AnthropicLlmService : ILlmService
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

    // == CompleteAsync == //

    public async Task<LlmResponse> CompleteAsync(CompletionRequest request, CancellationToken ct = default)
    {
        var model = request.Tier == ModelTier.Accurate ? _options.AccurateModel : _options.FastModel;

        try
        {
            var messages = request.Messages.Select(m => new MessageParam
            {
                Role    = m.Role == MessageRole.User ? Role.User : Role.Assistant,
                Content = m.Content
            }).ToList();

            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model     = model,
                MaxTokens = request.MaxTokens,
                System    = request.SystemPrompt,
                Messages  = messages
            }, ct);

            return new LlmResponse
            {
                Content           = ExtractTextContent(response),
                InputTokensUsed   = (int)response.Usage.InputTokens,
                OutputTokensUsed  = (int)response.Usage.OutputTokens,
                Model             = model,
                ContextWindowSize = _options.ContextWindow,
                WasTruncated      = response.StopReason == "max_tokens"
            };
        }
        catch (Exception ex) when (ex is not AiServiceException)
        {
            _logger.LogError(ex, "Anthropic API call failed during {Feature}", request.Feature);
            throw new AiServiceException($"Failed during {request.Feature}. Please try again.", ex);
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
