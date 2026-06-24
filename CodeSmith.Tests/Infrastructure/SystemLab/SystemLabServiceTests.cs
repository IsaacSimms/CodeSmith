// == System Lab Service Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Core.Models.PromptLab;
using CodeSmith.Core.Models.SystemLab;
using CodeSmith.Infrastructure.Services.SystemLab;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CodeSmith.Tests.Infrastructure.SystemLab;

public class SystemLabServiceTests
{
    private readonly ISystemLabSessionStore        _sessionStore = Substitute.For<ISystemLabSessionStore>();
    private readonly ISystemLabEvaluator           _evaluator    = Substitute.For<ISystemLabEvaluator>();
    private readonly IGuidanceConversation         _guidance     = Substitute.For<IGuidanceConversation>();
    private readonly ILogger<SystemLabService>     _logger       = Substitute.For<ILogger<SystemLabService>>();
    private readonly SystemLabService              _service;

    public SystemLabServiceTests()
    {
        _sessionStore.GetLock(Arg.Any<string>()).Returns(new SemaphoreSlim(1, 1)); // Required for all submit/chat paths
        _service = new SystemLabService(_evaluator, _guidance, _sessionStore, _logger);
    }

    // == Catalog Tests == //

    [Fact]
    public void GetScenarios_ReturnsNonEmptyList()
    {
        var scenarios = _service.GetScenarios();

        Assert.NotEmpty(scenarios);
    }

    [Fact]
    public void GetScenario_WithValidId_ReturnsScenario()
    {
        var firstId = _service.GetScenarios()[0].ScenarioId;

        var scenario = _service.GetScenario(firstId);

        Assert.Equal(firstId, scenario.ScenarioId);
    }

    [Fact]
    public void GetScenario_WithInvalidId_ThrowsScenarioNotFoundException()
    {
        Assert.Throws<ScenarioNotFoundException>(
            () => _service.GetScenario("does-not-exist"));
    }

    // == StartSessionAsync Tests == //

    [Fact]
    public async Task StartSessionAsync_WithValidId_CreatesAndStoresSession()
    {
        var scenarioId = _service.GetScenarios()[0].ScenarioId;

        var session = await _service.StartSessionAsync(scenarioId, AiProvider.Anthropic);

        Assert.Equal(scenarioId, session.ScenarioId);
        Assert.NotEqual(Guid.Empty, session.SessionId);
        _sessionStore.Received(1).Set(Arg.Is<SystemLabSession>(s => s.ScenarioId == scenarioId));
    }

    [Fact]
    public async Task StartSessionAsync_WithInvalidId_ThrowsScenarioNotFoundException()
    {
        await Assert.ThrowsAsync<ScenarioNotFoundException>(
            () => _service.StartSessionAsync("does-not-exist", AiProvider.Anthropic));
    }

    [Fact]
    public async Task StartSessionAsync_InitializesEmptyAttemptsAndChatHistory()
    {
        var scenarioId = _service.GetScenarios()[0].ScenarioId;

        var session = await _service.StartSessionAsync(scenarioId, AiProvider.Anthropic);

        Assert.Empty(session.Attempts);
        Assert.Empty(session.ChatHistory);
    }

    // == SubmitAttemptAsync Tests == //

    [Fact]
    public async Task SubmitAttemptAsync_WithUnknownSession_ThrowsSessionNotFoundException()
    {
        _sessionStore.Get(Arg.Any<string>()).Returns((SystemLabSession?)null);

        await Assert.ThrowsAsync<SessionNotFoundException>(
            () => _service.SubmitAttemptAsync(Guid.NewGuid(), "my justification"));
    }

    [Fact]
    public async Task SubmitAttemptAsync_StoresAttemptInSession()
    {
        var scenarioId = _service.GetScenarios()[0].ScenarioId;
        var session    = new SystemLabSession { ScenarioId = scenarioId };

        _sessionStore.Get(session.SessionId.ToString()).Returns(session);

        var expectedAttempt = new ScenarioAttempt
        {
            TotalScore      = 8,
            MaxScore        = 10,
            OverallFeedback = "Good reasoning on the core tradeoffs."
        };

        _evaluator.EvaluateAsync(Arg.Any<Scenario>(), Arg.Any<string>(), Arg.Any<AiProvider>(), Arg.Any<CancellationToken>())
            .Returns(expectedAttempt);

        var attempt = await _service.SubmitAttemptAsync(session.SessionId, "my justification");

        Assert.Equal(8, attempt.TotalScore);
        _sessionStore.Received(1).Set(Arg.Is<SystemLabSession>(s => s.Attempts.Count == 1));
    }

    [Fact]
    public async Task SubmitAttemptAsync_MultipleAttempts_AccumulatesInSession()
    {
        var scenarioId = _service.GetScenarios()[0].ScenarioId;
        var session    = new SystemLabSession { ScenarioId = scenarioId };

        _sessionStore.Get(session.SessionId.ToString()).Returns(session);

        _evaluator.EvaluateAsync(Arg.Any<Scenario>(), Arg.Any<string>(), Arg.Any<AiProvider>(), Arg.Any<CancellationToken>())
            .Returns(new ScenarioAttempt { TotalScore = 5, MaxScore = 10 });

        await _service.SubmitAttemptAsync(session.SessionId, "first attempt");
        await _service.SubmitAttemptAsync(session.SessionId, "second attempt");

        Assert.Equal(2, session.Attempts.Count);
    }

    [Fact]
    public async Task SubmitAttemptAsync_WhenEvaluatorThrowsParseException_SessionAttemptsNotMutated()
    {
        var scenarioId = _service.GetScenarios()[0].ScenarioId;
        var session    = new SystemLabSession { ScenarioId = scenarioId };

        _sessionStore.Get(session.SessionId.ToString()).Returns(session);
        _evaluator.EvaluateAsync(Arg.Any<Scenario>(), Arg.Any<string>(), Arg.Any<AiProvider>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ScenarioAttempt>(new EvaluationParseException("Malformed evaluator response")));

        await Assert.ThrowsAsync<AiServiceException>(
            () => _service.SubmitAttemptAsync(session.SessionId, "my justification"));

        Assert.Empty(session.Attempts);
    }

    // == ChatAsync Tests == //

    [Fact]
    public async Task ChatAsync_WithUnknownSession_ThrowsSessionNotFoundException()
    {
        _sessionStore.Get(Arg.Any<string>()).Returns((SystemLabSession?)null);

        await Assert.ThrowsAsync<SessionNotFoundException>(
            () => _service.ChatAsync(Guid.NewGuid(), "help me", null));
    }

    // The append/persist/rollback turn mechanics now live in GuidanceConversation (see
    // GuidanceConversationTests); the orchestrator's job is only to route the right history, provider,
    // and SystemLab:Chat feature and return the reply.
    [Fact]
    public async Task ChatAsync_DelegatesToGuidanceWithSessionHistoryAndReturnsContent()
    {
        var scenarioId = _service.GetScenarios()[0].ScenarioId;
        var session    = new SystemLabSession { ScenarioId = scenarioId, Provider = AiProvider.Anthropic };

        _sessionStore.Get(session.SessionId.ToString()).Returns(session);

        _guidance.RunTurnAsync(Arg.Any<AiProvider>(), Arg.Any<List<ChatMessage>>(), Arg.Any<GuidanceTurnRequest>(), Arg.Any<Action>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "Consider the RTO implications." });

        var response = await _service.ChatAsync(session.SessionId, "what about failover?", "my draft justification");

        Assert.Equal("Consider the RTO implications.", response);
        await _guidance.Received(1).RunTurnAsync(
            session.Provider,
            session.ChatHistory,
            Arg.Is<GuidanceTurnRequest>(r => r.Feature == "SystemLab:Chat" && r.UserMessage == "what about failover?"),
            Arg.Any<Action>(),
            Arg.Any<CancellationToken>());
    }
}
