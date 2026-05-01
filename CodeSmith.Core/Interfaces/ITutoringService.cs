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

    // Sends a guided assistance message within an existing session
    Task<ChatResponse> GetGuidanceAsync(Guid sessionId, string userMessage, string? editorContent = null, bool isCodeAnalysis = false, CancellationToken ct = default);
}
