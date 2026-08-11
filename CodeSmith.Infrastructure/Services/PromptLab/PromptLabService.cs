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
    private readonly IGuidanceConversation _guidance;
    private readonly ILogger<PromptLabService> _logger;

    private const int ChatMaxTokens = 1024; // Response token budget for guidance replies

    public PromptLabService(
        IPromptSimulator simulator,
        IPromptEvaluator evaluator,
        ITestInputGenerator generator,
        IPromptLabSessionStore sessionStore,
        IGuidanceConversation guidance,
        ILogger<PromptLabService> logger)
    {
        _simulator    = simulator;
        _evaluator    = evaluator;
        _generator    = generator;
        _sessionStore = sessionStore;
        _guidance     = guidance;
        _logger       = logger;
    }

    // == Catalog Operations (synchronous, in-memory) == //

    public IReadOnlyList<Challenge> GetChallenges() => ChallengeCatalog.All;

    public Challenge GetChallenge(string challengeId)
    {
        var challenge = ChallengeCatalog.All.FirstOrDefault(c => c.ChallengeId == challengeId);
        return challenge ?? throw new ChallengeNotFoundException(challengeId);
    }

    public async Task<PromptLabSession> StartChallengeAsync(string challengeId, AiProvider provider, CancellationToken ct = default)
    {
        var challenge = GetChallenge(challengeId); // Validates the ID — throws ChallengeNotFoundException if invalid

        bool dynamicInputsGenerated;
        List<TestInput> testInputs;
        try
        {
            testInputs             = await _generator.GenerateAsync(challenge, provider, ct);
            dynamicInputsGenerated = true;
        }
        catch (InsufficientQuotaException)
        {
            // Out of credits is not a "generation quality" failure — do not lie with static inputs.
            throw;
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
        // Serialize per session: the attempt mutates the shared Attempts list, so it must not interleave
        // with another submit or a guidance turn on the same session.
        return await _sessionStore.WithSessionLockAsync(sessionId.ToString(), async () =>
        {
            var session = _sessionStore.Get(sessionId.ToString())
                ?? throw new SessionNotFoundException(sessionId);

            var challenge  = GetChallenge(session.ChallengeId);
            var testInputs = session.TestInputs.Count > 0 ? session.TestInputs : challenge.TestInputs;

            _logger.LogInformation("Processing attempt for session {SessionId}, challenge {ChallengeId}", sessionId, challenge.ChallengeId);

            try
            {
                // Pipeline per input: each test input's simulate→evaluate chain is one task and all
                // chains run in parallel, so wall clock is the slowest single chain rather than
                // slowest-simulate + slowest-evaluate (the old sequential phases).
                var chains = testInputs.Select(async input =>
                {
                    var simulated = await _simulator.SimulateOneAsync(challenge, input, systemPromptContent, userMessageContent, session.Provider, ct);
                    var result    = await _evaluator.EvaluateOneAsync(challenge, input, simulated.Output, userMessageContent, session.Provider, ct);
                    return (Simulated: simulated, Result: result);
                }).ToList();

                var evaluated = await Task.WhenAll(chains);

                var attempt = _evaluator.AssembleAttempt(challenge, systemPromptContent, userMessageContent, [.. evaluated.Select(e => e.Result)]);

                // All simulation calls share the same prompt — first chain's token count is representative
                attempt.PromptTokensUsed  = evaluated.Length > 0 ? evaluated[0].Simulated.PromptTokens      : 0;
                attempt.ContextWindowSize = evaluated.Length > 0 ? evaluated[0].Simulated.ContextWindowSize : 0;

                session.Attempts.Add(attempt);
                _sessionStore.Set(session);

                _logger.LogInformation("Attempt complete for session {SessionId}: {Score}/{Max}", sessionId, attempt.TotalScore, attempt.MaxScore);
                return attempt;
            }
            catch (Exception ex)
            {
                // Domain signals (incl. quota from a parallel chain inside AggregateException) keep
                // their HTTP mapping. Only unknown failures become a uniform evaluate 502.
                if (TryUnwrapDomainException(ex, out var domain))
                    throw domain;

                _logger.LogError(ex, "Failed to process attempt for session {SessionId}", sessionId);
                throw new AiServiceException("Failed to evaluate prompt attempt. Please try again.", ex);
            }
        }, ct);
    }

    // == Domain Exception Passthrough == //

    // Passthrough set: InsufficientQuotaException, AiServiceException, OperationCanceledException,
    // SessionNotFoundException, ChallengeNotFoundException. AggregateException is flattened so a
    // quota failure on one parallel simulate/evaluate chain still surfaces as 402.
    private static bool TryUnwrapDomainException(Exception ex, out Exception domain)
    {
        if (IsDomainException(ex))
        {
            domain = ex;
            return true;
        }

        if (ex is AggregateException aggregate)
        {
            foreach (var inner in aggregate.Flatten().InnerExceptions)
            {
                if (IsDomainException(inner))
                {
                    domain = inner;
                    return true;
                }
            }
        }

        domain = ex;
        return false;
    }

    private static bool IsDomainException(Exception ex)
        => ex is InsufficientQuotaException
            or AiServiceException
            or OperationCanceledException
            or SessionNotFoundException
            or ChallengeNotFoundException;

    // == ChatAsync / StreamChatAsync == //

    public Task<string> ChatAsync(Guid sessionId, string message, string? editorContent, CancellationToken ct = default)
        => ExecuteChatAsync(sessionId, message, editorContent, onDelta: null, ct);

    public Task<string> StreamChatAsync(Guid sessionId, string message, string? editorContent,
        Func<string, CancellationToken, Task> onDelta, CancellationToken ct = default)
        => ExecuteChatAsync(sessionId, message, editorContent, onDelta, ct);

    // The turn mechanics (per-session lock, load-or-throw, streaming dispatch, persist, rollback) live
    // behind IGuidanceConversation — this surface supplies only its prompt data. The catalog lookup runs
    // inside buildTurn, so ChallengeNotFoundException still propagates with its own HTTP mapping.
    private async Task<string> ExecuteChatAsync(Guid sessionId, string message, string? editorContent,
        Func<string, CancellationToken, Task>? onDelta, CancellationToken ct)
    {
        var response = await _guidance.RunTurnAsync(_sessionStore, sessionId, session => new GuidanceTurnRequest
        {
            SystemPrompt = BuildChatSystemPrompt(GetChallenge(session.ChallengeId), session, editorContent),
            UserMessage  = message,
            MaxTokens    = ChatMaxTokens,
            Feature      = "PromptLab:Chat"
        }, onDelta, ct);

        return response.Content;
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
}
