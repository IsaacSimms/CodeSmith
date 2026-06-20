// == Prompt Lab Service == //
using System.Text;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Core.Models.PromptLab;
using Microsoft.Extensions.Logging;

namespace CodeSmith.Infrastructure.Services.PromptLab;

/// <summary>
/// Orchestrates the Prompt Lab workflow: catalog lookup, test-input generation, simulation, and evaluation.
/// Delegates each phase to a dedicated module and manages session persistence.
/// </summary>
public class PromptLabService : IPromptLabService
{
    private readonly IPromptSimulator    _simulator;
    private readonly IPromptEvaluator    _evaluator;
    private readonly ITestInputGenerator _generator;
    private readonly IPromptLabSessionStore _sessionStore;
    private readonly ILlmServiceFactory  _factory;
    private readonly ILogger<PromptLabService> _logger;

    private const int ChatHistoryWindow = 20;   // Max turns retained before trimming
    private const int ChatMaxTokens     = 1024; // Response token budget for guidance replies

    public PromptLabService(
        IPromptSimulator simulator,
        IPromptEvaluator evaluator,
        ITestInputGenerator generator,
        IPromptLabSessionStore sessionStore,
        ILlmServiceFactory factory,
        ILogger<PromptLabService> logger)
    {
        _simulator    = simulator;
        _evaluator    = evaluator;
        _generator    = generator;
        _sessionStore = sessionStore;
        _factory      = factory;
        _logger       = logger;
    }

    // == Catalog Operations (synchronous, in-memory) == //

    public IReadOnlyList<Challenge> GetChallenges() => ChallengeCatalog.All;

    public Challenge GetChallenge(string challengeId)
    {
        var challenge = ChallengeCatalog.All.FirstOrDefault(c => c.ChallengeId == challengeId);
        return challenge ?? throw new ChallengeNotFoundException(challengeId);
    }

    public async Task<PromptLabSession> StartChallengeAsync(string challengeId, AiProvider provider = AiProvider.Anthropic, CancellationToken ct = default)
    {
        var challenge = GetChallenge(challengeId); // Validates the ID — throws ChallengeNotFoundException if invalid

        bool dynamicInputsGenerated;
        List<TestInput> testInputs;
        try
        {
            testInputs             = await _generator.GenerateAsync(challenge, provider, ct);
            dynamicInputsGenerated = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Test input generation failed for {ChallengeId}; falling back to static inputs", challengeId);
            testInputs             = challenge.TestInputs;
            dynamicInputsGenerated = false;
        }

        var session = new PromptLabSession { ChallengeId = challengeId, Provider = provider, TestInputs = testInputs, DynamicInputsGenerated = dynamicInputsGenerated };
        _sessionStore.Set(session);

        _logger.LogInformation("Started session {SessionId} for {ChallengeId} with {Count} test inputs", session.SessionId, challengeId, testInputs.Count);
        return session;
    }

    // == Attempt Submission (async, 2 API phases) == //

    public async Task<ChallengeAttempt> SubmitAttemptAsync(
        Guid sessionId,
        string systemPromptContent,
        string userMessageContent,
        CancellationToken ct = default)
    {
        var session = _sessionStore.Get(sessionId.ToString())
            ?? throw new SessionNotFoundException(sessionId);

        var challenge  = GetChallenge(session.ChallengeId);
        var testInputs = session.TestInputs.Count > 0 ? session.TestInputs : challenge.TestInputs;

        _logger.LogInformation("Processing attempt for session {SessionId}, challenge {ChallengeId}", sessionId, challenge.ChallengeId);

        try
        {
            var simulation = await _simulator.SimulateAsync(challenge, testInputs, systemPromptContent, userMessageContent, session.Provider, ct);
            var attempt    = await _evaluator.EvaluateAsync(challenge, systemPromptContent, userMessageContent, simulation, session.Provider, ct);

            attempt.PromptTokensUsed  = simulation.PromptTokens;
            attempt.ContextWindowSize = simulation.ContextWindowSize;

            session.Attempts.Add(attempt);
            _sessionStore.Set(session);

            _logger.LogInformation("Attempt complete for session {SessionId}: {Score}/{Max}", sessionId, attempt.TotalScore, attempt.MaxScore);
            return attempt;
        }
        catch (Exception ex) when (ex is not AiServiceException and not SessionNotFoundException and not ChallengeNotFoundException)
        {
            _logger.LogError(ex, "Failed to process attempt for session {SessionId}", sessionId);
            throw new AiServiceException("Failed to evaluate prompt attempt. Please try again.", ex);
        }
    }

    // == ChatAsync == //

    public async Task<string> ChatAsync(Guid sessionId, string message, string? editorContent, CancellationToken ct = default)
    {
        var session   = _sessionStore.Get(sessionId.ToString()) ?? throw new SessionNotFoundException(sessionId);
        var challenge = GetChallenge(session.ChallengeId);

        // Append user turn before calling LLM so history is current
        session.ChatHistory.Add(new ChatMessage { Role = MessageRole.User, Content = message });
        TrimHistoryIfNeeded(session);

        var systemPrompt = BuildChatSystemPrompt(challenge, session, editorContent);

        try
        {
            var response = await _factory.Get(session.Provider).CompleteAsync(new CompletionRequest
            {
                SystemPrompt = systemPrompt,
                Messages     = session.ChatHistory,
                Tier         = ModelTier.Fast,
                MaxTokens    = ChatMaxTokens,
                Feature      = "PromptLab:Chat"
            }, ct);

            session.ChatHistory.Add(new ChatMessage { Role = MessageRole.Assistant, Content = response.Content });
            _sessionStore.Set(session);

            return response.Content;
        }
        catch (Exception ex) when (ex is not AiServiceException and not SessionNotFoundException and not ChallengeNotFoundException)
        {
            // Remove the optimistically added user turn so history stays consistent on failure
            session.ChatHistory.RemoveAt(session.ChatHistory.Count - 1);
            _logger.LogError(ex, "Failed to get guidance for session {SessionId}", sessionId);
            throw new AiServiceException("Failed to get guidance. Please try again.", ex);
        }
    }

    // == Chat Prompt Builder == //

    private static string BuildChatSystemPrompt(Challenge challenge, PromptLabSession session, string? editorContent)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are an adaptive prompt engineering tutor. Your job is to guide the student toward writing better prompts for the challenge below.");
        sb.AppendLine();
        sb.AppendLine("Default approach: Be Socratic — ask probing questions, highlight gaps, and point to relevant principles. Do NOT hand-feed the solution.");
        sb.AppendLine("Exceptions: If the student explicitly asks for a direct answer, provide it clearly. If they have submitted 3 or more failed attempts, shift to direct, constructive feedback.");
        sb.AppendLine("Keep responses concise — two to three focused sentences or questions at most.");
        sb.AppendLine();

        sb.AppendLine($"Challenge: {challenge.Title}");
        sb.AppendLine("Description:");
        sb.AppendLine(challenge.Description.Trim());
        sb.AppendLine();

        sb.AppendLine("Scoring Rubric (what the student is graded on):");
        foreach (var criterion in challenge.Rubric)
            sb.AppendLine($"  - {criterion.Name}: {criterion.Description} ({criterion.MaxPoints} pts)");
        sb.AppendLine();

        sb.AppendLine("Editable fields (what the student is writing):");
        foreach (var field in challenge.EditableFields)
            sb.AppendLine($"  - {field.FieldType}: {field.Placeholder}");
        sb.AppendLine();

        if (session.Attempts.Count > 0)
        {
            var last = session.Attempts[^1];
            sb.AppendLine($"Most recent attempt: {last.TotalScore}/{last.MaxScore} points.");
            sb.AppendLine("Per-criterion breakdown:");
            foreach (var result in last.Results)
            foreach (var score in result.CriterionScores)
                sb.AppendLine($"  - {score.CriterionName}: {score.Points}/{score.MaxPoints}");
            sb.AppendLine($"Overall feedback: {last.OverallFeedback.Trim()}");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(editorContent))
        {
            sb.AppendLine("Student's current prompt draft:");
            sb.AppendLine(editorContent.Trim());
            sb.AppendLine();
        }

        sb.AppendLine("Guide the student. Do not reveal hidden test inputs or adversarial conditions.");
        return sb.ToString();
    }

    // == Helpers == //

    private static void TrimHistoryIfNeeded(PromptLabSession session)
    {
        while (session.ChatHistory.Count > ChatHistoryWindow)
            session.ChatHistory.RemoveAt(0);
    }
}
