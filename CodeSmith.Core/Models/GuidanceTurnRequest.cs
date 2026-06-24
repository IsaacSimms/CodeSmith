// == Guidance Turn Request == //
namespace CodeSmith.Core.Models;

/// <summary>
/// The turn-shaped data for a single Guidance Turn: the surface-built system prompt, the new user
/// message, the output budget, the history window, and the usage Feature label. The session wiring
/// (provider, history list, persistence) is passed to IGuidanceConversation.RunTurnAsync separately —
/// it is not turn data. Tier is intentionally absent: a Guidance Turn is always Fast-tier, an
/// invariant owned by the Module rather than chosen per call.
/// </summary>
public sealed record GuidanceTurnRequest
{
    public required string SystemPrompt { get; init; }   // Surface-built Socratic system prompt for this turn
    public required string UserMessage { get; init; }    // The student's new message, appended to history before the call
    public required int MaxTokens { get; init; }          // Output token budget for the guidance reply
    public required int MaxTurns { get; init; }           // Max messages retained; older turns are trimmed before the call
    public required string Feature { get; init; }         // Usage/ledger label (e.g. "Tutoring:Guidance")
}
