// == System Lab Service Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Core.Models.PromptLab;
using CodeSmith.Core.Models.SystemLab;
using CodeSmith.Infrastructure.Services;
using CodeSmith.Infrastructure.Services.SystemLab;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CodeSmith.Tests.Infrastructure.SystemLab;

public class SystemLabServiceTests
{
    private readonly ISystemLabSessionStore        _sessionStore = Substitute.For<ISystemLabSessionStore>();
    private readonly ISystemLabEvaluator           _evaluator    = Substitute.For<ISystemLabEvaluator>();
    private readonly ILlmService                   _llm          = Substitute.For<ILlmService>();
    private readonly ILogger<SystemLabService>     _logger       = Substitute.For<ILogger<SystemLabService>>();
    private readonly SystemLabService              _service;

    public SystemLabServiceTests()
    {
        // Pass-through the per-session lock so submit/chat bodies run inline (the lock itself is covered
        // by InMemorySessionStoreTests). Chat turns lock at the LlmResponse level inside GuidanceConversation.
        _sessionStore.WithSessionLockAsync(Arg.Any<string>(), Arg.Any<Func<Task<ScenarioAttempt>>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Func<Task<ScenarioAttempt>>>()());
        _sessionStore.WithSessionLockAsync(Arg.Any<string>(), Arg.Any<Func<Task<LlmResponse>>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Func<Task<LlmResponse>>>()());

        // Chat runs through the real GuidanceConversation over the substituted ILlmService, so chat tests
        // observe orchestrator behavior through the surface's own Interface.
        var factory = Substitute.For<ILlmServiceFactory>();
        factory.Get(Arg.Any<AiProvider>()).Returns(_llm);
        var guidance = new GuidanceConversation(factory, Substitute.For<ILogger<GuidanceConversation>>());

        _service = new SystemLabService(_evaluator, guidance, _sessionStore, _logger);
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

    [Fact]
    public async Task SubmitAttemptAsync_WhenEvaluatorThrowsInsufficientQuota_RethrowsWithoutWrapping()
    {
        var scenarioId = _service.GetScenarios()[0].ScenarioId;
        var session    = new SystemLabSession { ScenarioId = scenarioId };

        _sessionStore.Get(session.SessionId.ToString()).Returns(session);
        var original = new InsufficientQuotaException("user-1", "Insufficient quota or credits for this request.");
        _evaluator.EvaluateAsync(Arg.Any<Scenario>(), Arg.Any<string>(), Arg.Any<AiProvider>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ScenarioAttempt>(original));

        var thrown = await Assert.ThrowsAsync<InsufficientQuotaException>(
            () => _service.SubmitAttemptAsync(session.SessionId, "my justification"));

        Assert.Same(original, thrown);
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

    // The turn mechanics live in GuidanceConversation (see GuidanceConversationTests); these cover the
    // orchestrator's own job: building the scenario-aware prompt data with the SystemLab:Chat feature
    // and returning the reply content, with the turn landing on the session's chat history.
    [Fact]
    public async Task ChatAsync_RunsTurnOnSessionHistoryAndReturnsContent()
    {
        var scenarioId = _service.GetScenarios()[0].ScenarioId;
        var session    = new SystemLabSession { ScenarioId = scenarioId, Provider = AiProvider.Anthropic };

        _sessionStore.Get(session.SessionId.ToString()).Returns(session);

        string? feature = null;
        _llm.CompleteAsync(Arg.Do<CompletionRequest>(r => feature = r.Feature), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "Consider the RTO implications." });

        var response = await _service.ChatAsync(session.SessionId, "what about failover?", "my draft justification");

        Assert.Equal("Consider the RTO implications.", response);
        Assert.Equal("SystemLab:Chat", feature);
        Assert.Equal(2, session.ChatHistory.Count);           // whole turn landed on the session's chat history
        Assert.Equal("what about failover?",            session.ChatHistory[0].Content);
        Assert.Equal("Consider the RTO implications.",  session.ChatHistory[1].Content);
        _sessionStore.Received(1).Set(session);
    }

    [Fact]
    public async Task StreamChatAsync_StreamsTheReplyThroughCallerOnDelta()
    {
        var scenarioId = _service.GetScenarios()[0].ScenarioId;
        var session    = new SystemLabSession { ScenarioId = scenarioId, Provider = AiProvider.Anthropic };
        _sessionStore.Get(session.SessionId.ToString()).Returns(session);

        _llm.StreamAsync(Arg.Any<CompletionRequest>(), Arg.Any<Func<string, CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var push = callInfo.Arg<Func<string, CancellationToken, Task>>();
                await push("Streamed ", CancellationToken.None);
                await push("reply",     CancellationToken.None);
                return new LlmResponse { Content = "Streamed reply" };
            });

        var deltas = new List<string>();
        var response = await _service.StreamChatAsync(session.SessionId, "what about failover?", null,
            (text, _) => { deltas.Add(text); return Task.CompletedTask; });

        Assert.Equal("Streamed reply", response);
        Assert.Equal(["Streamed ", "reply"], deltas);         // the caller's callback received the deltas unwrapped
        Assert.Equal(2, session.ChatHistory.Count);
    }
}
