// == OpenAI-Compatible LLM Service Implementation == //
using System.ClientModel;
using System.ClientModel.Primitives;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

namespace CodeSmith.Infrastructure.Services;

/// <summary>
/// Implementation of <see cref="ILlmService"/> using the official OpenAI .NET SDK. Drives any
/// OpenAI-compatible endpoint: OpenAI itself (default endpoint) and xAI/Grok (custom base URL).
/// The only thing that varies across those providers is the endpoint + credential and the model
/// names — passed in at construction — so one adapter covers both. Stateless: no session management.
/// </summary>
public class OpenAiCompatibleLlmService : ILlmService
{
    private readonly OpenAIClient _client;
    private readonly AiProvider _provider;
    private readonly string _accurateModel;
    private readonly string _fastModel;
    private readonly int _contextWindow;
    private readonly ILogger _logger;
    private readonly TimeSpan _streamIdleTimeout;
    private readonly TimeSpan _streamTotalTimeout;

    /// <param name="endpoint">Base URL for an OpenAI-compatible endpoint (e.g. xAI). Null/empty uses the OpenAI default.</param>
    /// <param name="httpClient">Optional HTTP transport override — the internal seam the adapter's own tests use. Null uses the SDK default.</param>
    public OpenAiCompatibleLlmService(
        AiProvider provider,
        string apiKey,
        string accurateModel,
        string fastModel,
        int contextWindow,
        string? endpoint,
        ILogger logger,
        HttpClient? httpClient = null,
        int timeoutSeconds = 120,
        int streamIdleTimeoutSeconds = 30,
        int streamTotalTimeoutSeconds = 300)
    {
        _provider           = provider;
        _accurateModel      = accurateModel;
        _fastModel          = fastModel;
        _contextWindow      = contextWindow;
        _logger             = logger;
        _streamIdleTimeout  = TimeSpan.FromSeconds(streamIdleTimeoutSeconds);
        _streamTotalTimeout = TimeSpan.FromSeconds(streamTotalTimeoutSeconds);
        NetworkTimeout      = TimeSpan.FromSeconds(timeoutSeconds);

        // Explicit timeout and no pipeline auto-retry — a retried metered completion burns
        // provider cost invisibly and multiplies worst-case latency.
        var clientOptions = new OpenAIClientOptions
        {
            NetworkTimeout = NetworkTimeout,
            RetryPolicy    = new ClientRetryPolicy(maxRetries: 0)
        };
        if (!string.IsNullOrWhiteSpace(endpoint))
            clientOptions.Endpoint = new Uri(endpoint);
        if (httpClient is not null)
            clientOptions.Transport = new HttpClientPipelineTransport(httpClient);

        _client = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);
    }

    internal TimeSpan NetworkTimeout { get; }   // Test-only view of the configured pipeline timeout

    // == CompleteAsync == //

    public async Task<LlmResponse> CompleteAsync(CompletionRequest request, CancellationToken ct = default)
    {
        var model = request.Tier == ModelTier.Accurate ? _accurateModel : _fastModel;

        try
        {
            var messages = new List<OpenAI.Chat.ChatMessage> { new SystemChatMessage(request.SystemPrompt) };
            foreach (var m in request.Messages)
            {
                messages.Add(m.Role == MessageRole.User
                    ? new UserChatMessage(m.Content)
                    : (OpenAI.Chat.ChatMessage)new AssistantChatMessage(m.Content));
            }

            var chatClient = _client.GetChatClient(model);
            var response   = await chatClient.CompleteChatAsync(
                messages,
                new ChatCompletionOptions { MaxOutputTokenCount = request.MaxTokens },
                ct);

            return new LlmResponse
            {
                Content           = ExtractTextContent(response.Value),
                InputTokensUsed   = response.Value.Usage.InputTokenCount,
                OutputTokensUsed  = response.Value.Usage.OutputTokenCount,
                Model             = model,
                ContextWindowSize = _contextWindow,
                WasTruncated      = response.Value.FinishReason == ChatFinishReason.Length
            };
        }
        // Caller-initiated cancellation must propagate as OperationCanceledException (maps to 499,
        // not 502); a provider-side timeout is also an OCE but with an un-cancelled token, so it still wraps.
        catch (Exception ex) when (ex is not AiServiceException
                                   && !(ex is OperationCanceledException && ct.IsCancellationRequested))
        {
            _logger.LogError(ex, "{Provider} API call failed during {Feature}", _provider, request.Feature);
            throw new AiServiceException($"{_provider} call failed during {request.Feature}. Please try again.", ex);
        }
    }

    // == StreamAsync == //

    public async Task<LlmResponse> StreamAsync(CompletionRequest request, Func<string, CancellationToken, Task> onDelta, CancellationToken ct = default)
    {
        var model = request.Tier == ModelTier.Accurate ? _accurateModel : _fastModel;

        try
        {
            var messages = new List<OpenAI.Chat.ChatMessage> { new SystemChatMessage(request.SystemPrompt) };
            foreach (var m in request.Messages)
            {
                messages.Add(m.Role == MessageRole.User
                    ? new UserChatMessage(m.Content)
                    : (OpenAI.Chat.ChatMessage)new AssistantChatMessage(m.Content));
            }

            using var guard = new StreamGuard(ct, _streamIdleTimeout, _streamTotalTimeout);

            var content = new System.Text.StringBuilder();
            ChatFinishReason? finishReason = null;
            var inputTokens  = 0;
            var outputTokens = 0;

            var chatClient = _client.GetChatClient(model);
            await foreach (var update in chatClient.CompleteChatStreamingAsync(
                messages,
                new ChatCompletionOptions { MaxOutputTokenCount = request.MaxTokens },
                guard.Token))
            {
                guard.Pulse();

                foreach (var part in update.ContentUpdate)
                {
                    if (string.IsNullOrEmpty(part.Text)) continue;
                    content.Append(part.Text);
                    await onDelta(part.Text, ct);
                }

                if (update.FinishReason is not null) finishReason = update.FinishReason;

                // Usage rides the final chunk only (stream_options.include_usage) — settle-on-actuals depends on it
                if (update.Usage is not null)
                {
                    inputTokens  = update.Usage.InputTokenCount;
                    outputTokens = update.Usage.OutputTokenCount;
                }
            }

            return new LlmResponse
            {
                Content           = content.ToString(),
                InputTokensUsed   = inputTokens,
                OutputTokensUsed  = outputTokens,
                Model             = model,
                ContextWindowSize = _contextWindow,
                WasTruncated      = finishReason == ChatFinishReason.Length
            };
        }
        // Same filter as CompleteAsync — but a StreamGuard timeout is an OCE with an un-cancelled
        // caller token, so it wraps to AiServiceException instead of masquerading as a 499.
        catch (Exception ex) when (ex is not AiServiceException
                                   && !(ex is OperationCanceledException && ct.IsCancellationRequested))
        {
            _logger.LogError(ex, "{Provider} streaming call failed during {Feature}", _provider, request.Feature);
            throw new AiServiceException($"{_provider} call failed during {request.Feature}. Please try again.", ex);
        }
    }

    // == Helpers == //

    private static string ExtractTextContent(ChatCompletion response)
        => response.Content.Count > 0 ? response.Content[0].Text : string.Empty;
}
