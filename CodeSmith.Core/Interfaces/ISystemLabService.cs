// == System Lab Service Interface == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Models.SystemLab;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Defines operations for the System Lab feature: browsing the scenario catalog,
/// starting sessions, submitting justification attempts for evaluation, and guidance chat.
/// </summary>
public interface ISystemLabService
{
    IReadOnlyList<Scenario> GetScenarios();

    // Returns a single scenario by ID; throws ScenarioNotFoundException if not found
    Scenario GetScenario(string scenarioId);

    // Creates a new System Lab session for the given scenario; throws ScenarioNotFoundException if invalid
    Task<SystemLabSession> StartSessionAsync(string scenarioId, AiProvider provider, CancellationToken ct = default);

    // Evaluates the user's justification against the scenario rubric and tradeoffs; throws SessionNotFoundException if invalid
    Task<ScenarioAttempt> SubmitAttemptAsync(Guid sessionId, string justificationContent, CancellationToken ct = default);

    // Sends a chat message to the guidance AI with current draft context; throws SessionNotFoundException if invalid
    Task<string> ChatAsync(Guid sessionId, string message, string? currentJustification, CancellationToken ct = default);
}
