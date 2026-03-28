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
    /// <summary>
    /// Generates a new coding problem at the specified difficulty level.
    /// </summary>
    /// <param name="difficulty">The desired difficulty level.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A new <see cref="ProblemSession"/> containing the problem and starter code.</returns>
    Task<ProblemSession> GenerateProblemAsync(Difficulty difficulty, CancellationToken ct = default);

    /// <summary>
    /// Sends a user message within an existing session and returns guided assistance.
    /// Maintains full conversation history for context.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="userMessage">The user's message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The assistant's guidance response.</returns>
    Task<string> GetGuidanceAsync(Guid sessionId, string userMessage, CancellationToken ct = default);
}
