// == Tutoring Service Interface == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Models;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Session-aware tutoring orchestration. Owns prompt templates, conversation history,
/// and session lifecycle. Delegates raw completions to ILlmService.
/// </summary>
public interface ITutoringService
{
    // Generates a new coding problem session using the specified provider
    Task<ProblemSession> GenerateProblemAsync(Difficulty difficulty, Language language, AiProvider provider, CancellationToken ct = default);

    // Streaming sibling of GenerateProblemAsync: description text streams through onDescriptionDelta
    // as it is written; onReset fires before a retry attempt so shown text can be cleared. The stored
    // session (with parsed starter code) arrives only in the returned value.
    Task<ProblemSession> StreamGenerateProblemAsync(
        Difficulty difficulty, Language language, AiProvider provider,
        Func<string, CancellationToken, Task> onDescriptionDelta,
        Func<CancellationToken, Task> onReset,
        CancellationToken ct = default);

    // Sends a guided assistance message within an existing session
    Task<ChatResponse> GetGuidanceAsync(Guid sessionId, string userMessage, string? editorContent = null, GuidanceMode guidanceMode = GuidanceMode.Guidance, CancellationToken ct = default);

    // Streaming sibling of GetGuidanceAsync: the reply streams through onDelta; the returned
    // ChatResponse still carries the full text plus the context-token metadata the UI shows
    Task<ChatResponse> StreamGuidanceAsync(
        Guid sessionId, string userMessage, string? editorContent, GuidanceMode guidanceMode,
        Func<string, CancellationToken, Task> onDelta,
        CancellationToken ct = default);

    // Executes user code for an existing session, validating the session exists
    Task<CodeExecutionResult> RunCodeAsync(Guid sessionId, Language language, string code, CancellationToken ct = default);
}
