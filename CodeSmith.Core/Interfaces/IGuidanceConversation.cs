// == Guidance Conversation Seam == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Models;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Owns one round of a multi-turn Socratic Guidance Conversation, shared by every surface (Tutoring,
/// Prompt Lab, System Lab). A single deep Module concentrates the history-mutation and error invariant
/// that the three orchestrators used to hand-copy (and diverge on): append the user message, trim the
/// history to a whole-turn window anchored on a User message, run one Fast-tier Completion, append the
/// assistant reply, and persist. On non-domain failure it rolls the optimistically-added user turn back
/// and surfaces <see cref="Exceptions.AiServiceException"/>. Each surface supplies only its system prompt
/// (as data) and the session wiring; the turn mechanics live here.
/// </summary>
public interface IGuidanceConversation
{
    /// <summary>
    /// Runs one Guidance Turn against <paramref name="history"/> (mutated in place), routing the
    /// Completion to <paramref name="provider"/> and invoking <paramref name="persist"/> after the
    /// history is updated. Returns the raw completion so callers can project token usage as needed.
    /// </summary>
    Task<LlmResponse> RunTurnAsync(
        AiProvider provider,
        List<ChatMessage> history,
        GuidanceTurnRequest request,
        Action persist,
        CancellationToken ct = default);
}
