// == Prompt Lab Service Interface == //
using CodeSmith.Core.Models.PromptLab;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Defines operations for the Prompt Lab feature: browsing the challenge catalog,
/// starting sessions, and submitting prompt attempts for simulation and evaluation.
/// </summary>
public interface IPromptLabService
{
    IReadOnlyList<Challenge> GetChallenges();  // Returns the full curated challenge catalog

    // Returns a single challenge by ID; throws ChallengeNotFoundException if not found
    Challenge GetChallenge(string challengeId);

    // Creates a new Prompt Lab session for the specified challenge; throws ChallengeNotFoundException if invalid
    PromptLabSession StartChallenge(string challengeId);

    // Runs the user's prompt against all test inputs (simulation) and evaluates the results; throws SessionNotFoundException if invalid
    Task<ChallengeAttempt> SubmitAttemptAsync(
        Guid sessionId,
        string systemPromptContent,
        string userMessageContent,
        CancellationToken ct = default);
}
