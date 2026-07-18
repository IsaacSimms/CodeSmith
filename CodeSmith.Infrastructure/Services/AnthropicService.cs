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

    /// <param name="httpClient">Optional HTTP transport override — the internal seam the adapter's own tests use. Null uses the SDK default.</param>
    public AnthropicLlmService(
        IOptions<AnthropicOptions> options,
        ILogger<AnthropicLlmService> logger,
        HttpClient? httpClient = null)
    {
        _options = options.Value;

        // Explicit timeout (SDK default is 10 minutes) and no transport auto-retry — a retried
        // metered completion burns provider cost invisibly and multiplies worst-case latency.
        var timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        _client = httpClient is null
            ? new AnthropicClient { ApiKey = _options.ApiKey, Timeout = timeout, MaxRetries = _options.MaxRetries }
            : new AnthropicClient { ApiKey = _options.ApiKey, Timeout = timeout, MaxRetries = _options.MaxRetries, HttpClient = httpClient };

        _logger  = logger;
    }

    internal AnthropicClient Client => _client;   // Test-only view of the configured SDK client

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
        // Caller-initiated cancellation must propagate as OperationCanceledException (maps to 499,
        // not 502); a provider-side timeout is also an OCE but with an un-cancelled token, so it still wraps.
        catch (Exception ex) when (ex is not AiServiceException
                                   && !(ex is OperationCanceledException && ct.IsCancellationRequested))
        {
            _logger.LogError(ex, "Anthropic API call failed during {Feature}", request.Feature);
            throw new AiServiceException($"Failed during {request.Feature}. Please try again.", ex);
        }
    }

    // == StreamAsync == //

    public async Task<LlmResponse> StreamAsync(CompletionRequest request, Func<string, CancellationToken, Task> onDelta, CancellationToken ct = default)
    {
        var model = request.Tier == ModelTier.Accurate ? _options.AccurateModel : _options.FastModel;

        try
        {
            var messages = request.Messages.Select(m => new MessageParam
            {
                Role    = m.Role == MessageRole.User ? Role.User : Role.Assistant,
                Content = m.Content
            }).ToList();

            using var guard = new StreamGuard(ct,
                TimeSpan.FromSeconds(_options.StreamIdleTimeoutSeconds),
                TimeSpan.FromSeconds(_options.StreamTotalTimeoutSeconds));

            var content = new System.Text.StringBuilder();
            var wasTruncated  = false;
            long inputTokens  = 0;
            long outputTokens = 0;

            await foreach (var streamEvent in _client.Messages.CreateStreaming(new MessageCreateParams
            {
                Model     = model,
                MaxTokens = request.MaxTokens,
                System    = request.SystemPrompt,
                Messages  = messages
            }, guard.Token))
            {
                guard.Pulse();

                if (streamEvent.TryPickContentBlockDelta(out var blockDelta) && blockDelta.Delta.TryPickText(out var textDelta))
                {
                    content.Append(textDelta.Text);
                    await onDelta(textDelta.Text, ct);
                }
                else if (streamEvent.TryPickStart(out var start))
                {
                    inputTokens = start.Message.Usage.InputTokens;   // input side of usage rides message_start
                }
                else if (streamEvent.TryPickDelta(out var messageDelta))
                {
                    outputTokens = messageDelta.Usage.OutputTokens;  // output side + stop reason ride message_delta
                    wasTruncated = messageDelta.Delta.StopReason is { } stopReason && (string)stopReason == "max_tokens";
                }
            }

            return new LlmResponse
            {
                Content           = content.ToString(),
                InputTokensUsed   = (int)inputTokens,
                OutputTokensUsed  = (int)outputTokens,
                Model             = model,
                ContextWindowSize = _options.ContextWindow,
                WasTruncated      = wasTruncated
            };
        }
        // Same filter as CompleteAsync — but a StreamGuard timeout is an OCE with an un-cancelled
        // caller token, so it wraps to AiServiceException instead of masquerading as a 499.
        catch (Exception ex) when (ex is not AiServiceException
                                   && !(ex is OperationCanceledException && ct.IsCancellationRequested))
        {
            _logger.LogError(ex, "Anthropic streaming call failed during {Feature}", request.Feature);
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
