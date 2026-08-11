// == Guidance Conversation Seam == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Models;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Owns one round of a multi-turn Socratic Guidance Conversation, shared by every surface (Tutoring,
/// Prompt Lab, System Lab). A single deep Module concentrates the whole turn frame the three
/// orchestrators used to hand-copy (and diverge on): take the store's per-session lock, load the
/// session or throw, append the user message, trim the history to a whole-turn window anchored on a
/// User message, run one Fast-tier Completion (blocking or streaming), append the assistant reply, and
/// persist. On non-domain failure it rolls the optimistically-added user turn back and surfaces
/// <see cref="Exceptions.AiServiceException"/>. Each surface supplies only a buildTurn callback that
/// turns its loaded session into <see cref="GuidanceTurnRequest"/> data; the turn mechanics live here.
/// </summary>
public interface IGuidanceConversation
{
    // Runs one Guidance Turn against the session identified by sessionId: takes the store's
    // per-session lock, loads the session (or throws SessionNotFoundException), invokes buildTurn
    // with the loaded session to obtain the turn data, runs the turn against the session's
    // GuidanceHistory, and persists via store.Set on success. Passing onDelta selects the streaming
    // shape — the assistant reply is pushed through it as it is generated, under the same invariant
    // (history gains the whole turn or neither message, never a partial assistant message).
    // buildTurn failures (e.g. a catalog lookup throwing) propagate unwrapped and mutate nothing.
    Task<LlmResponse> RunTurnAsync<TSession>(
        ISessionStore<TSession> store,
        Guid sessionId,
        Func<TSession, GuidanceTurnRequest> buildTurn,
        Func<string, CancellationToken, Task>? onDelta = null,
        CancellationToken ct = default)
        where TSession : class, IGuidanceSession;
}
