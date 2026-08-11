// == Prompt Lab Service Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Core.Models.PromptLab;
using CodeSmith.Infrastructure.Services;
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
    private readonly ILlmService                _llm          = Substitute.For<ILlmService>();
    private readonly ILogger<PromptLabService>  _logger       = Substitute.For<ILogger<PromptLabService>>();
    private readonly PromptLabService           _service;

    public PromptLabServiceTests()
    {
        // Pass-through the per-session lock so submit/chat bodies run inline (the lock itself is covered
        // by InMemorySessionStoreTests). Chat turns lock at the LlmResponse level inside GuidanceConversation.
        _sessionStore.WithSessionLockAsync(Arg.Any<string>(), Arg.Any<Func<Task<ChallengeAttempt>>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Func<Task<ChallengeAttempt>>>()());
        _sessionStore.WithSessionLockAsync(Arg.Any<string>(), Arg.Any<Func<Task<LlmResponse>>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Func<Task<LlmResponse>>>()());

        // Chat runs through the real GuidanceConversation over the substituted ILlmService, so chat tests
        // observe orchestrator behavior through the surface's own Interface.
        var factory = Substitute.For<ILlmServiceFactory>();
        factory.Get(Arg.Any<AiProvider>()).Returns(_llm);
        var guidance = new GuidanceConversation(factory, Substitute.For<ILogger<GuidanceConversation>>());

        _service = new PromptLabService(_simulator, _evaluator, _generator, _sessionStore, guidance, _logger);
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

        var session = await _service.StartChallengeAsync(challengeId, AiProvider.Anthropic);

        Assert.Equal(challengeId, session.ChallengeId);
        Assert.NotEqual(Guid.Empty, session.SessionId);
        _sessionStore.Received(1).Set(Arg.Is<PromptLabSession>(s => s.ChallengeId == challengeId));
    }

    [Fact]
    public async Task StartChallengeAsync_WithInvalidId_ThrowsChallengeNotFoundException()
    {
        await Assert.ThrowsAsync<ChallengeNotFoundException>(
            () => _service.StartChallengeAsync("does-not-exist", AiProvider.Anthropic));
    }

    [Fact]
    public async Task StartChallengeAsync_InitializesEmptyAttemptsList()
    {
        var challengeId = _service.GetChallenges()[0].ChallengeId;

        _generator.GenerateAsync(Arg.Any<Challenge>(), Arg.Any<AiProvider>(), Arg.Any<CancellationToken>())
            .Returns<List<TestInput>>(x => throw new InvalidOperationException("LLM unavailable"));

        var session = await _service.StartChallengeAsync(challengeId, AiProvider.Anthropic);

        Assert.Empty(session.Attempts);
    }

    [Fact]
    public async Task StartChallengeAsync_SessionHasTestInputs()
    {
        var challengeId = _service.GetChallenges()[0].ChallengeId;

        // Generator fails; fallback ensures static inputs are always present
        _generator.GenerateAsync(Arg.Any<Challenge>(), Arg.Any<AiProvider>(), Arg.Any<CancellationToken>())
            .Returns<List<TestInput>>(x => throw new InvalidOperationException("LLM unavailable"));

        var session = await _service.StartChallengeAsync(challengeId, AiProvider.Anthropic);

        Assert.NotEmpty(session.TestInputs);
    }

    [Fact]
    public async Task StartChallengeAsync_WhenGeneratorSucceeds_SetsDynamicInputsGeneratedTrue()
    {
        var challengeId    = _service.GetChallenges()[0].ChallengeId;
        var dynamicInputs  = new List<TestInput> { new() { InputId = "d1", Label = "Dynamic 1" } };

        _generator.GenerateAsync(Arg.Any<Challenge>(), Arg.Any<AiProvider>(), Arg.Any<CancellationToken>())
            .Returns(dynamicInputs);

        var session = await _service.StartChallengeAsync(challengeId, AiProvider.Anthropic);

        Assert.True(session.DynamicInputsGenerated);
    }

    [Fact]
    public async Task StartChallengeAsync_WhenGeneratorFails_SetsDynamicInputsGeneratedFalse()
    {
        var challengeId = _service.GetChallenges()[0].ChallengeId;

        _generator.GenerateAsync(Arg.Any<Challenge>(), Arg.Any<AiProvider>(), Arg.Any<CancellationToken>())
            .Returns<List<TestInput>>(x => throw new InvalidOperationException("LLM unavailable"));

        var session = await _service.StartChallengeAsync(challengeId, AiProvider.Anthropic);

        Assert.False(session.DynamicInputsGenerated);
    }

    [Fact]
    public async Task StartChallengeAsync_WhenGeneratorThrowsInsufficientQuota_DoesNotFallbackToStatic()
    {
        var challengeId = _service.GetChallenges()[0].ChallengeId;
        var original = new InsufficientQuotaException("user-1", "Insufficient quota or credits for this request.");

        _generator.GenerateAsync(Arg.Any<Challenge>(), Arg.Any<AiProvider>(), Arg.Any<CancellationToken>())
            .Returns<List<TestInput>>(_ => throw original);

        var thrown = await Assert.ThrowsAsync<InsufficientQuotaException>(
            () => _service.StartChallengeAsync(challengeId, AiProvider.Anthropic));

        Assert.Same(original, thrown);
        _sessionStore.DidNotReceive().Set(Arg.Any<PromptLabSession>());
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

        var expectedAttempt = new ChallengeAttempt
        {
            SystemPromptContent = "be concise",
            UserMessageContent  = "list planets",
            TotalScore          = 2,
            MaxScore            = 4,
            OverallFeedback     = "0/1 test inputs passed (50% of available points)."
        };

        MockSimulateOne();
        MockEvaluateOne();
        _evaluator.AssembleAttempt(Arg.Any<Challenge>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<TestInputResult>>())
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

        MockSimulateOne(promptTokens: 77, contextWindow: 180_000);
        MockEvaluateOne();
        _evaluator.AssembleAttempt(Arg.Any<Challenge>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<TestInputResult>>())
            .Returns(new ChallengeAttempt());

        var attempt = await _service.SubmitAttemptAsync(session.SessionId, "sys", "user", CancellationToken.None);

        Assert.Equal(77,      attempt.PromptTokensUsed);
        Assert.Equal(180_000, attempt.ContextWindowSize);
    }

    [Fact]
    public async Task SubmitAttemptAsync_EvaluatesEveryTestInput_AndAssemblesThoseResults()
    {
        var challengeId = _service.GetChallenges()[0].ChallengeId;
        var inputs      = new List<TestInput>
        {
            new() { InputId = "i1", Label = "One", UserMessage = "a" },
            new() { InputId = "i2", Label = "Two", UserMessage = "b" },
            new() { InputId = "i3", Label = "Three", UserMessage = "c" }
        };
        var session = new PromptLabSession { ChallengeId = challengeId, Provider = AiProvider.Anthropic, TestInputs = inputs };

        _sessionStore.Get(session.SessionId.ToString()).Returns(session);

        MockSimulateOne();
        MockEvaluateOne();
        _evaluator.AssembleAttempt(Arg.Any<Challenge>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<TestInputResult>>())
            .Returns(new ChallengeAttempt());

        await _service.SubmitAttemptAsync(session.SessionId, "sys", "user", CancellationToken.None);

        await _simulator.Received(3).SimulateOneAsync(Arg.Any<Challenge>(), Arg.Any<TestInput>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<AiProvider>(), Arg.Any<CancellationToken>());
        await _evaluator.Received(3).EvaluateOneAsync(Arg.Any<Challenge>(), Arg.Any<TestInput>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<AiProvider>(), Arg.Any<CancellationToken>());
        _evaluator.Received(1).AssembleAttempt(Arg.Any<Challenge>(), "sys", "user", Arg.Is<IReadOnlyList<TestInputResult>>(r => r.Count == 3));
    }

    [Fact]
    public async Task SubmitAttemptAsync_PipelinesPerInput_EvaluationStartsBeforeAllSimulationsFinish()
    {
        // Input ONE's simulation completes only after input TWO's evaluation has STARTED. Under the
        // old sequential phases (all simulations, then all evaluations) this deadlocks; it can only
        // complete when each input's simulate→evaluate chain runs independently.
        var challengeId = _service.GetChallenges()[0].ChallengeId;
        var inputOne    = new TestInput { InputId = "i1", Label = "One", UserMessage = "a" };
        var inputTwo    = new TestInput { InputId = "i2", Label = "Two", UserMessage = "b" };
        var session     = new PromptLabSession { ChallengeId = challengeId, Provider = AiProvider.Anthropic, TestInputs = [inputOne, inputTwo] };

        _sessionStore.Get(session.SessionId.ToString()).Returns(session);

        var evalTwoStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _simulator.SimulateOneAsync(Arg.Any<Challenge>(), inputOne, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<AiProvider>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                await evalTwoStarted.Task;   // gate: released only once input TWO reaches evaluation
                return new SimulatedInput(inputOne, "out-1", 10, 200_000);
            });
        _simulator.SimulateOneAsync(Arg.Any<Challenge>(), inputTwo, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<AiProvider>(), Arg.Any<CancellationToken>())
            .Returns(new SimulatedInput(inputTwo, "out-2", 10, 200_000));

        _evaluator.EvaluateOneAsync(Arg.Any<Challenge>(), Arg.Any<TestInput>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<AiProvider>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                if (ci.Arg<TestInput>().InputId == "i2")
                    evalTwoStarted.TrySetResult();
                return new TestInputResult { InputId = ci.Arg<TestInput>().InputId, Label = "r" };
            });

        _evaluator.AssembleAttempt(Arg.Any<Challenge>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<TestInputResult>>())
            .Returns(new ChallengeAttempt());

        var submit = _service.SubmitAttemptAsync(session.SessionId, "sys", "user", CancellationToken.None);

        // A generous timeout so a regression to sequential phases fails fast instead of hanging the run
        var completed = await Task.WhenAny(submit, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(submit, completed);
        await submit;
    }

    [Fact]
    public async Task SubmitAttemptAsync_WhenSimulatorThrowsInsufficientQuota_RethrowsWithoutWrapping()
    {
        var challengeId = _service.GetChallenges()[0].ChallengeId;
        var session = new PromptLabSession
        {
            ChallengeId = challengeId,
            Provider = AiProvider.Anthropic,
            TestInputs = [new TestInput { InputId = "i1", Label = "One", UserMessage = "a" }],
        };
        _sessionStore.Get(session.SessionId.ToString()).Returns(session);

        var original = new InsufficientQuotaException("user-1", "Insufficient quota or credits for this request.");
        _simulator.SimulateOneAsync(Arg.Any<Challenge>(), Arg.Any<TestInput>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<AiProvider>(), Arg.Any<CancellationToken>())
            .Returns<SimulatedInput>(_ => throw original);

        var thrown = await Assert.ThrowsAsync<InsufficientQuotaException>(
            () => _service.SubmitAttemptAsync(session.SessionId, "sys", "user", CancellationToken.None));

        Assert.Same(original, thrown);
        Assert.Empty(session.Attempts);
    }

    // == Submit Mock Helpers == //

    private void MockSimulateOne(int promptTokens = 10, int contextWindow = 200_000)
        => _simulator.SimulateOneAsync(Arg.Any<Challenge>(), Arg.Any<TestInput>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<AiProvider>(), Arg.Any<CancellationToken>())
            .Returns(ci => new SimulatedInput(ci.Arg<TestInput>(), "output", promptTokens, contextWindow));

    private void MockEvaluateOne()
        => _evaluator.EvaluateOneAsync(Arg.Any<Challenge>(), Arg.Any<TestInput>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<AiProvider>(), Arg.Any<CancellationToken>())
            .Returns(ci => new TestInputResult { InputId = ci.Arg<TestInput>().InputId, Label = "r" });

    // == ChatAsync Tests == //

    [Fact]
    public async Task ChatAsync_WithUnknownSession_ThrowsSessionNotFoundException()
    {
        _sessionStore.Get(Arg.Any<string>()).Returns((PromptLabSession?)null);

        await Assert.ThrowsAsync<SessionNotFoundException>(
            () => _service.ChatAsync(Guid.NewGuid(), "help me", null, CancellationToken.None));
    }

    // Turn mechanics live in GuidanceConversation (see GuidanceConversationTests); these cover the
    // orchestrator's own job: building the challenge-aware prompt data with the PromptLab:Chat feature
    // and returning the reply content, with the turn landing on the session's chat history.
    [Fact]
    public async Task ChatAsync_RunsTurnOnSessionHistoryAndReturnsContent()
    {
        var challengeId = _service.GetChallenges()[0].ChallengeId;
        var session     = new PromptLabSession { ChallengeId = challengeId, Provider = AiProvider.Anthropic };

        _sessionStore.Get(session.SessionId.ToString()).Returns(session);

        string? feature = null;
        _llm.CompleteAsync(Arg.Do<CompletionRequest>(r => feature = r.Feature), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "What does the rubric reward?" });

        var response = await _service.ChatAsync(session.SessionId, "how do I improve?", "my draft prompt", CancellationToken.None);

        Assert.Equal("What does the rubric reward?", response);
        Assert.Equal("PromptLab:Chat", feature);
        Assert.Equal(2, session.ChatHistory.Count);           // whole turn landed on the session's chat history
        Assert.Equal("how do I improve?",             session.ChatHistory[0].Content);
        Assert.Equal("What does the rubric reward?",  session.ChatHistory[1].Content);
        _sessionStore.Received(1).Set(session);
    }

    [Fact]
    public async Task StreamChatAsync_StreamsTheReplyThroughCallerOnDelta()
    {
        var challengeId = _service.GetChallenges()[0].ChallengeId;
        var session     = new PromptLabSession { ChallengeId = challengeId, Provider = AiProvider.Anthropic };
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
        var response = await _service.StreamChatAsync(session.SessionId, "how do I improve?", null,
            (text, _) => { deltas.Add(text); return Task.CompletedTask; }, CancellationToken.None);

        Assert.Equal("Streamed reply", response);
        Assert.Equal(["Streamed ", "reply"], deltas);         // the caller's callback received the deltas unwrapped
        Assert.Equal(2, session.ChatHistory.Count);
    }
}
