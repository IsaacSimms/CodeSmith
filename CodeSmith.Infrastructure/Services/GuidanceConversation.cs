// == Guidance Conversation Implementation == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeSmith.Infrastructure.Services;

/// <summary>
/// The one Module that owns a Guidance Turn for every surface. It concentrates the history-mutation
/// and error invariant the three orchestrators used to hand-copy: append the user message, trim to a
/// whole-turn window anchored on a User message, run one Fast-tier Completion via the provider's
/// usage-enforced ILlmService, append the assistant reply, and persist. Any non-domain failure rolls
/// the optimistic user turn back and is surfaced as AiServiceException; AiServiceException and
/// cancellation pass through untouched (the latter so it still maps to 499, not 502).
/// </summary>
public sealed class GuidanceConversation : IGuidanceConversation
{
    private readonly ILlmServiceFactory _factory;
    private readonly ILogger<GuidanceConversation> _logger;

    public GuidanceConversation(ILlmServiceFactory factory, ILogger<GuidanceConversation> logger)
    {
        _factory = factory;
        _logger  = logger;
    }

    // == RunTurnAsync == //

    public async Task<LlmResponse> RunTurnAsync(
        AiProvider provider,
        List<ChatMessage> history,
        GuidanceTurnRequest request,
        Action persist,
        CancellationToken ct = default)
    {
        // Append the user turn optimistically so the model sees the current message, then bound the window
        history.Add(new ChatMessage { Role = MessageRole.User, Content = request.UserMessage });
        TrimToWindow(history, request.MaxTurns);

        try
        {
            var response = await _factory.Get(provider).CompleteAsync(new CompletionRequest
            {
                SystemPrompt = request.SystemPrompt,
                Messages     = history,
                Tier         = ModelTier.Fast,
                MaxTokens    = request.MaxTokens,
                Feature      = request.Feature
            }, ct);

            history.Add(new ChatMessage { Role = MessageRole.Assistant, Content = response.Content });
            persist();

            return response;
        }
        catch (Exception ex)
        {
            // Roll back the optimistic user turn on any failure so history stays consistent
            RollBackTrailingUserTurn(history);

            // AiServiceException is already the clean domain shape; cancellation must keep its own
            // mapping (→ 499). Everything else is wrapped so the surface gets a uniform 502.
            if (ex is AiServiceException or OperationCanceledException)
                throw;

            _logger.LogError(ex, "Guidance turn failed for feature {Feature}", request.Feature);
            throw new AiServiceException("Failed to get guidance. Please try again.", ex);
        }
    }

    // == History Window == //

    // Keeps history within MaxTurns messages, dropping whole leading turns so the window stays anchored
    // on a User message — the providers require the first message to be a User turn.
    private static void TrimToWindow(List<ChatMessage> history, int maxTurns)
    {
        while (history.Count > maxTurns)
            history.RemoveAt(0);

        // A front trim can orphan a leading Assistant message; drop it so the window opens on a User turn.
        if (history.Count > 0 && history[0].Role == MessageRole.Assistant)
            history.RemoveAt(0);
    }

    private static void RollBackTrailingUserTurn(List<ChatMessage> history)
    {
        if (history.Count > 0 && history[^1].Role == MessageRole.User)
            history.RemoveAt(history.Count - 1);
    }
}
