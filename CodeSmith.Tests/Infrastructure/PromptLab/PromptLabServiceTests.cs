// == Prompt Lab Service Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models.PromptLab;
using CodeSmith.Infrastructure.Services.PromptLab;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CodeSmith.Tests.Infrastructure.PromptLab;

public class PromptLabServiceTests
{
    private readonly IPromptLabSessionStore     _sessionStore = Substitute.For<IPromptLabSessionStore>();
    private readonly IPromptSimulator           _simulator    = Substitute.For<IPromptSimulator>();
    private readonly IPromptEvaluator           _evaluator    = Substitute.For<IPromptEvaluator>();
    private readonly ITestInputGenerator        _generator    = Substitute.For<ITestInputGenerator>();
    private readonly ILogger<PromptLabService>  _logger       = Substitute.For<ILogger<PromptLabService>>();
    private readonly PromptLabService           _service;

    public PromptLabServiceTests()
    {
        _service = new PromptLabService(_simulator, _evaluator, _generator, _sessionStore, _logger);
    }

    // == Catalog Tests == //

    [Fact]
    public void GetChallenges_ReturnsNonEmptyList()
    {
        var challenges = _service.GetChallenges();

        Assert.NotEmpty(challenges);
    }

    [Fact]
    public void GetChallenge_WithValidId_ReturnsChallenge()
    {
        var firstId = _service.GetChallenges()[0].ChallengeId;

        var challenge = _service.GetChallenge(firstId);

        Assert.Equal(firstId, challenge.ChallengeId);
    }

    [Fact]
    public void GetChallenge_WithInvalidId_ThrowsChallengeNotFoundException()
    {
        Assert.Throws<ChallengeNotFoundException>(
            () => _service.GetChallenge("does-not-exist"));
    }

    // == StartChallengeAsync Tests == //

    [Fact]
    public async Task StartChallengeAsync_WithValidId_CreatesAndStoresSession()
    {
        var challengeId = _service.GetChallenges()[0].ChallengeId;

        // Generator throws — orchestrator falls back to static inputs
        _generator.GenerateAsync(Arg.Any<Challenge>(), Arg.Any<AiProvider>(), Arg.Any<CancellationToken>())
            .Returns<List<TestInput>>(x => throw new InvalidOperationException("LLM unavailable"));

        var session = await _service.StartChallengeAsync(challengeId);

        Assert.Equal(challengeId, session.ChallengeId);
        Assert.NotEqual(Guid.Empty, session.SessionId);
        _sessionStore.Received(1).Set(Arg.Is<PromptLabSession>(s => s.ChallengeId == challengeId));
    }

    [Fact]
    public async Task StartChallengeAsync_WithInvalidId_ThrowsChallengeNotFoundException()
    {
        await Assert.ThrowsAsync<ChallengeNotFoundException>(
            () => _service.StartChallengeAsync("does-not-exist"));
    }

    [Fact]
    public async Task StartChallengeAsync_InitializesEmptyAttemptsList()
    {
        var challengeId = _service.GetChallenges()[0].ChallengeId;

        _generator.GenerateAsync(Arg.Any<Challenge>(), Arg.Any<AiProvider>(), Arg.Any<CancellationToken>())
            .Returns<List<TestInput>>(x => throw new InvalidOperationException("LLM unavailable"));

        var session = await _service.StartChallengeAsync(challengeId);

        Assert.Empty(session.Attempts);
    }

    [Fact]
    public async Task StartChallengeAsync_SessionHasTestInputs()
    {
        var challengeId = _service.GetChallenges()[0].ChallengeId;

        // Generator fails; fallback ensures static inputs are always present
        _generator.GenerateAsync(Arg.Any<Challenge>(), Arg.Any<AiProvider>(), Arg.Any<CancellationToken>())
            .Returns<List<TestInput>>(x => throw new InvalidOperationException("LLM unavailable"));

        var session = await _service.StartChallengeAsync(challengeId);

        Assert.NotEmpty(session.TestInputs);
    }

    // == SubmitAttemptAsync Tests == //

    [Fact]
    public async Task SubmitAttemptAsync_WithUnknownSession_ThrowsSessionNotFoundException()
    {
        _sessionStore.Get(Arg.Any<string>()).Returns((PromptLabSession?)null);

        await Assert.ThrowsAsync<SessionNotFoundException>(
            () => _service.SubmitAttemptAsync(Guid.NewGuid(), "be concise", "list planets", CancellationToken.None));
    }

    [Fact]
    public async Task SubmitAttemptAsync_CompletesSuccessfully_StoresAttemptInSession()
    {
        var challengeId = _service.GetChallenges()[0].ChallengeId;
        var challenge   = _service.GetChallenge(challengeId);
        var session     = new PromptLabSession
        {
            ChallengeId = challengeId,
            Provider    = AiProvider.Anthropic,
            TestInputs  = challenge.TestInputs
        };

        _sessionStore.Get(session.SessionId.ToString()).Returns(session);

        var simulationResult = new SimulationResult(
            challenge.TestInputs.Select(i => (i, "output")).ToList(), 10, 200_000);

        var expectedAttempt = new ChallengeAttempt
        {
            SystemPromptContent = "be concise",
            UserMessageContent  = "list planets",
            TotalScore          = 2,
            MaxScore            = 4,
            OverallFeedback     = "0/1 test inputs passed (50% of available points)."
        };

        _simulator.SimulateAsync(Arg.Any<Challenge>(), Arg.Any<List<TestInput>>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<AiProvider>(), Arg.Any<CancellationToken>())
            .Returns(simulationResult);

        _evaluator.EvaluateAsync(Arg.Any<Challenge>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SimulationResult>(), Arg.Any<AiProvider>(), Arg.Any<CancellationToken>())
            .Returns(expectedAttempt);

        var attempt = await _service.SubmitAttemptAsync(session.SessionId, "be concise", "list planets", CancellationToken.None);

        Assert.Equal("be concise", attempt.SystemPromptContent);
        _sessionStore.Received(1).Set(Arg.Is<PromptLabSession>(s => s.Attempts.Count == 1));
    }

    [Fact]
    public async Task SubmitAttemptAsync_TokensFromSimulation_SetOnAttempt()
    {
        var challengeId = _service.GetChallenges()[0].ChallengeId;
        var challenge   = _service.GetChallenge(challengeId);
        var session     = new PromptLabSession { ChallengeId = challengeId, Provider = AiProvider.Anthropic, TestInputs = challenge.TestInputs };

        _sessionStore.Get(session.SessionId.ToString()).Returns(session);

        _simulator.SimulateAsync(Arg.Any<Challenge>(), Arg.Any<List<TestInput>>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<AiProvider>(), Arg.Any<CancellationToken>())
            .Returns(new SimulationResult(challenge.TestInputs.Select(i => (i, "out")).ToList(), PromptTokens: 77, ContextWindowSize: 180_000));

        _evaluator.EvaluateAsync(Arg.Any<Challenge>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SimulationResult>(), Arg.Any<AiProvider>(), Arg.Any<CancellationToken>())
            .Returns(new ChallengeAttempt());

        var attempt = await _service.SubmitAttemptAsync(session.SessionId, "sys", "user", CancellationToken.None);

        Assert.Equal(77,      attempt.PromptTokensUsed);
        Assert.Equal(180_000, attempt.ContextWindowSize);
    }
}
