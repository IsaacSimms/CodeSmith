// == Anthropic Service Interface == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Models;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Defines operations for interacting with the Anthropic Claude API
/// to generate coding problems and provide guided assistance.
/// </summary>
public interface IAnthropicService
{
    Task<ProblemSession> GenerateProblemAsync(Difficulty difficulty, Language language, CancellationToken ct = default);  // Generates a new coding problem at the specified difficulty level and language
    Task<string> GetGuidanceAsync(Guid sessionId, string userMessage, string? editorContent = null, bool isCodeAnalysis = false, CancellationToken ct = default);  // Sends a user message within an existing session and returns guided assistance
}
