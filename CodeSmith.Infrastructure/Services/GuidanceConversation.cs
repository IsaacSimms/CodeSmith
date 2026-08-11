// == Guidance Conversation Implementation == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeSmith.Infrastructure.Services;

/// <summary>
/// The one Module that owns a Guidance Turn for every surface. It concentrates the whole turn frame
/// the three orchestrators used to hand-copy: take the store's per-session lock, load the session or
/// throw SessionNotFoundException, append the user message, trim to a whole-turn window anchored on a
/// User message, run one Fast-tier Completion via the provider's usage-enforced ILlmService (blocking,
/// or streaming when onDelta is supplied), append the assistant reply, and persist via store.Set. Any
/// non-domain failure rolls the optimistic user turn back and is surfaced as AiServiceException;
/// AiServiceException, InsufficientQuotaException, and cancellation pass through untouched (the latter
/// so it still maps to 499, not 502). buildTurn failures propagate unwrapped and mutate nothing.
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

    // == Session-Level Turn == //

    // The whole turn — load, build, complete, persist — runs under the store's per-session lock: a
    // Guidance Turn mutates the session's shared history list, so concurrent turns on one session must
    // not interleave. A streaming turn holds the lock for its whole duration; partial turns are never
    // persisted. buildTurn runs before the optimistic append, so its failures propagate untouched.
    public Task<LlmResponse> RunTurnAsync<TSession>(
        ISessionStore<TSession> store,
        Guid sessionId,
        Func<TSession, GuidanceTurnRequest> buildTurn,
        Func<string, CancellationToken, Task>? onDelta = null,
        CancellationToken ct = default)
        where TSession : class, IGuidanceSession
        => store.WithSessionLockAsync(sessionId.ToString(), async () =>
        {
            var session = store.Get(sessionId.ToString())
                ?? throw new SessionNotFoundException(sessionId);

            _logger.LogInformation("Processing guidance turn for session {SessionId}", sessionId);
            var request = buildTurn(session);

            return await ExecuteTurnAsync(session.Provider, session.GuidanceHistory, request,
                persist: () => store.Set(session),
                invoke: onDelta is null
                    ? (llm, completion, token) => llm.CompleteAsync(completion, token)
                    : (llm, completion, token) => llm.StreamAsync(completion, onDelta, token),
                ct);
        }, ct);

    // == Turn Invariant Core == //

    // One implementation of append → trim → complete → append → persist (with whole-turn rollback on
    // failure) shared by both operation shapes, so the history invariant cannot drift between them.
    private async Task<LlmResponse> ExecuteTurnAsync(
        AiProvider provider,
        List<ChatMessage> history,
        GuidanceTurnRequest request,
        Action persist,
        Func<ILlmService, CompletionRequest, CancellationToken, Task<LlmResponse>> invoke,
        CancellationToken ct)
    {
        // Append the user turn optimistically so the model sees the current message, then bound the window
        history.Add(new ChatMessage { Role = MessageRole.User, Content = request.UserMessage });
        TrimToWindow(history, request.MaxTurns);

        try
        {
            var response = await invoke(_factory.Get(provider), new CompletionRequest
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
            // Roll back the optimistic user turn on any failure so history stays consistent — for a
            // stream that died mid-reply this discards the partial assistant text entirely: history
            // must never contain a partial assistant message (providers reject malformed alternation).
            RollBackTrailingUserTurn(history);

            // Domain signals and cancellation must keep their HTTP mapping (402 / 499 / existing
            // AiServiceException → 502). Only unknown failures are wrapped into a uniform guidance 502.
            // Passthrough: InsufficientQuotaException, AiServiceException, OperationCanceledException.
            if (ex is InsufficientQuotaException or AiServiceException or OperationCanceledException)
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
