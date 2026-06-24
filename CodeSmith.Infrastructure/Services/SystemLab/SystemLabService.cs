// == System Lab Service == //
using System.Text;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Core.Models.SystemLab;
using Microsoft.Extensions.Logging;

namespace CodeSmith.Infrastructure.Services.SystemLab;

/// <summary>
/// Orchestrates the System Lab workflow: catalog lookup, justification evaluation, and guidance chat.
/// Delegates evaluation to ISystemLabEvaluator and each guidance turn to IGuidanceConversation, while
/// retaining the per-session lock that also guards SubmitAttemptAsync.
/// </summary>
public class SystemLabService : ISystemLabService
{
    private readonly ISystemLabEvaluator     _evaluator;
    private readonly IGuidanceConversation   _guidance;
    private readonly ISystemLabSessionStore  _sessionStore;
    private readonly ILogger<SystemLabService> _logger;

    private const int ChatMaxTokens     = 800;
    private const int ChatHistoryWindow = 20;  // Max messages retained before older turns are trimmed

    public SystemLabService(
        ISystemLabEvaluator evaluator,
        IGuidanceConversation guidance,
        ISystemLabSessionStore sessionStore,
        ILogger<SystemLabService> logger)
    {
        _evaluator    = evaluator;
        _guidance     = guidance;
        _sessionStore = sessionStore;
        _logger       = logger;
    }

    // == Catalog Operations == //

    public IReadOnlyList<Scenario> GetScenarios() => ScenarioCatalog.All;

    public Scenario GetScenario(string scenarioId)
    {
        var scenario = ScenarioCatalog.All.FirstOrDefault(s => s.ScenarioId == scenarioId);
        return scenario ?? throw new ScenarioNotFoundException(scenarioId);
    }

    // == StartSessionAsync == //

    public Task<SystemLabSession> StartSessionAsync(string scenarioId, AiProvider provider, CancellationToken ct = default)
    {
        GetScenario(scenarioId); // Validates ID — throws ScenarioNotFoundException if invalid

        var session = new SystemLabSession { ScenarioId = scenarioId, Provider = provider };
        _sessionStore.Set(session);

        _logger.LogInformation("Started System Lab session {SessionId} for scenario {ScenarioId}", session.SessionId, scenarioId);
        return Task.FromResult(session);
    }

    // == SubmitAttemptAsync == //

    public async Task<ScenarioAttempt> SubmitAttemptAsync(Guid sessionId, string justificationContent, CancellationToken ct = default)
    {
        var session  = _sessionStore.Get(sessionId.ToString()) ?? throw new SessionNotFoundException(sessionId);
        var scenario = GetScenario(session.ScenarioId);

        _logger.LogInformation("Evaluating attempt for session {SessionId}, scenario {ScenarioId}", sessionId, scenario.ScenarioId);

        var semaphore = _sessionStore.GetLock(sessionId.ToString());
        await semaphore.WaitAsync(ct);
        try
        {
            var attempt = await _evaluator.EvaluateAsync(scenario, justificationContent, session.Provider, ct);

            session.Attempts.Add(attempt);
            _sessionStore.Set(session);

            _logger.LogInformation("Attempt complete for session {SessionId}: {Score}/{Max}", sessionId, attempt.TotalScore, attempt.MaxScore);
            return attempt;
        }
        catch (Exception ex) when (ex is not AiServiceException and not SessionNotFoundException and not ScenarioNotFoundException)
        {
            _logger.LogError(ex, "Failed to evaluate attempt for session {SessionId}", sessionId);
            throw new AiServiceException("Failed to evaluate justification. Please try again.", ex);
        }
        finally
        {
            semaphore.Release();
        }
    }

    // == ChatAsync == //

    public async Task<string> ChatAsync(Guid sessionId, string message, string? currentJustification, CancellationToken ct = default)
    {
        var session  = _sessionStore.Get(sessionId.ToString()) ?? throw new SessionNotFoundException(sessionId);
        var scenario = GetScenario(session.ScenarioId);

        // The per-session lock stays with the orchestrator: it also guards SubmitAttemptAsync, so it is
        // broader than a single guidance turn and cannot live inside the Guidance Conversation Module.
        var semaphore = _sessionStore.GetLock(sessionId.ToString());
        await semaphore.WaitAsync(ct);
        try
        {
            var systemPrompt = BuildChatSystemPrompt(scenario, currentJustification);
            var response = await _guidance.RunTurnAsync(session.Provider, session.ChatHistory, new GuidanceTurnRequest
            {
                SystemPrompt = systemPrompt,
                UserMessage  = message,
                MaxTokens    = ChatMaxTokens,
                MaxTurns     = ChatHistoryWindow,
                Feature      = "SystemLab:Chat"
            }, () => _sessionStore.Set(session), ct);

            return response.Content;
        }
        finally
        {
            semaphore.Release();
        }
    }

    // == Chat Prompt Builder == //

    private static string BuildChatSystemPrompt(Scenario scenario, string? currentJustification)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a Socratic infrastructure engineering tutor. Your role is to guide the student toward better reasoning.");
        sb.AppendLine("Do NOT give away the answer directly. Ask probing questions, point to relevant principles, and highlight gaps in reasoning.");
        sb.AppendLine("Be concise — one or two focused questions or observations per response.");
        sb.AppendLine();

        sb.AppendLine($"Scenario: {scenario.Title}");
        sb.AppendLine("Description:");
        sb.AppendLine(scenario.Description.Trim());
        sb.AppendLine();
        sb.AppendLine("Constraints:");
        sb.AppendLine(scenario.Constraints.Trim());
        sb.AppendLine();

        sb.AppendLine("Rubric Criteria (what the student is being graded on):");
        foreach (var criterion in scenario.Rubric)
            sb.AppendLine($"  - {criterion.Name}: {criterion.Description}");
        sb.AppendLine();

        sb.AppendLine("Required Tradeoffs (questions the student must engage with):");
        foreach (var tradeoff in scenario.RequiredTradeoffs)
            sb.AppendLine($"  - {tradeoff}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(currentJustification))
        {
            sb.AppendLine("Student's current draft justification:");
            sb.AppendLine(currentJustification.Trim());
            sb.AppendLine();
        }

        sb.AppendLine("Guide the student. Do not reveal the rubric scores or security pitfalls.");
        return sb.ToString();
    }

}
