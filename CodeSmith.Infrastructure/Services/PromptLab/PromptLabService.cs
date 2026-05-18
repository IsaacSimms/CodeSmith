// == Prompt Lab Service == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
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
    private readonly ILogger<PromptLabService> _logger;

    public PromptLabService(
        IPromptSimulator simulator,
        IPromptEvaluator evaluator,
        ITestInputGenerator generator,
        IPromptLabSessionStore sessionStore,
        ILogger<PromptLabService> logger)
    {
        _simulator    = simulator;
        _evaluator    = evaluator;
        _generator    = generator;
        _sessionStore = sessionStore;
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
}
