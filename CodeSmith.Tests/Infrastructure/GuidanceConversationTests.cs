// == Guidance Conversation Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CodeSmith.Tests.Infrastructure;

public class GuidanceConversationTests
{
    private readonly ILlmServiceFactory              _factory = Substitute.For<ILlmServiceFactory>();
    private readonly ILlmService                     _llm     = Substitute.For<ILlmService>();
    private readonly ILogger<GuidanceConversation>   _logger  = Substitute.For<ILogger<GuidanceConversation>>();
    private readonly GuidanceConversation            _conversation;

    public GuidanceConversationTests()
    {
        _factory.Get(Arg.Any<AiProvider>()).Returns(_llm);
        _conversation = new GuidanceConversation(_factory, _logger);
    }

    // == Helpers == //

    private static GuidanceTurnRequest Request(string user = "help me", int maxTokens = 1024, int maxTurns = 20, string feature = "Tutoring:Guidance")
        => new()
        {
            SystemPrompt = "You are a Socratic tutor.",
            UserMessage  = user,
            MaxTokens    = maxTokens,
            MaxTurns     = maxTurns,
            Feature      = feature
        };

    // Store substitute at the ISessionStore seam: lock passes through inline (the real lock is covered
    // by InMemorySessionStoreTests), Get serves the given session, Set/lock calls are countable.
    private static ISessionStore<ProblemSession> StoreWith(ProblemSession? session, Guid sessionId)
    {
        var store = Substitute.For<ISessionStore<ProblemSession>>();
        store.Get(sessionId.ToString()).Returns(session);
        store.WithSessionLockAsync(Arg.Any<string>(), Arg.Any<Func<Task<LlmResponse>>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Func<Task<LlmResponse>>>()());
        return store;
    }

    // == Lock, Load, Build, Persist == //

    [Fact]
    public async Task RunTurnAsync_Session_LoadsBuildsAppendsAndPersistsUnderTheSessionLock()
    {
        var session = new ProblemSession { Provider = AiProvider.Anthropic };
        var store   = StoreWith(session, session.SessionId);
        _llm.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "What have you tried?" });

        ProblemSession? builtFrom = null;
        var response = await _conversation.RunTurnAsync(store, session.SessionId, s =>
        {
            builtFrom = s;
            return Request(user: "I'm stuck");
        });

        Assert.Equal("What have you tried?", response.Content);
        Assert.Same(session, builtFrom);                       // turn data is built from the loaded session
        Assert.Equal(2, session.Messages.Count);               // whole turn landed on the session's history
        Assert.Equal(MessageRole.User,      session.Messages[0].Role);
        Assert.Equal(MessageRole.Assistant, session.Messages[1].Role);
        store.Received(1).Set(session);                        // persisted once, after the turn completed
        await store.Received(1).WithSessionLockAsync(          // the whole turn ran under the per-session lock
            session.SessionId.ToString(), Arg.Any<Func<Task<LlmResponse>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunTurnAsync_Session_WithUnknownSession_ThrowsSessionNotFoundWithoutCallingLlm()
    {
        var sessionId = Guid.NewGuid();
        var store     = StoreWith(session: null, sessionId);

        await Assert.ThrowsAsync<SessionNotFoundException>(
            () => _conversation.RunTurnAsync(store, sessionId, _ => Request()));

        await _llm.DidNotReceiveWithAnyArgs().CompleteAsync(default!, default);
        store.DidNotReceive().Set(Arg.Any<ProblemSession>());
    }

    [Fact]
    public async Task RunTurnAsync_Session_WhenBuildTurnThrows_PropagatesUnwrappedAndMutatesNothing()
    {
        // A surface's buildTurn may do catalog lookups (e.g. ChallengeNotFoundException) — those domain
        // signals must keep their own HTTP mapping and must not touch history or persistence.
        var session = new ProblemSession { Provider = AiProvider.Anthropic };
        var store   = StoreWith(session, session.SessionId);
        var domain  = new ChallengeNotFoundException("missing-challenge");

        var thrown = await Assert.ThrowsAsync<ChallengeNotFoundException>(
            () => _conversation.RunTurnAsync<ProblemSession>(store, session.SessionId, _ => throw domain));

        Assert.Same(domain, thrown);
        Assert.Empty(session.Messages);
        store.DidNotReceive().Set(Arg.Any<ProblemSession>());
        await _llm.DidNotReceiveWithAnyArgs().CompleteAsync(default!, default);
    }

    // == Completion Shape == //

    [Fact]
    public async Task RunTurnAsync_Session_SendsFastTierWithRequestFeatureMaxTokensAndCurrentHistory()
    {
        var session = new ProblemSession { Provider = AiProvider.Anthropic };
        var store   = StoreWith(session, session.SessionId);
        // Snapshot at call time: Messages aliases the live history list, which is mutated (assistant
        // appended) the instant the call returns. Capturing the reference would assert the post-call state.
        ModelTier? tier = null; string? feature = null; int maxTokens = 0; string? systemPrompt = null;
        (MessageRole Role, string Content)? lastMessage = null;
        _llm.CompleteAsync(Arg.Do<CompletionRequest>(r =>
        {
            tier         = r.Tier;
            feature      = r.Feature;
            maxTokens    = r.MaxTokens;
            systemPrompt = r.SystemPrompt;
            lastMessage  = (r.Messages[^1].Role, r.Messages[^1].Content);
        }), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "ok" });

        await _conversation.RunTurnAsync(store, session.SessionId,
            _ => Request(user: "why?", maxTokens: 800, feature: "SystemLab:Chat"));

        Assert.Equal(ModelTier.Fast, tier);
        Assert.Equal("SystemLab:Chat", feature);
        Assert.Equal(800, maxTokens);
        Assert.Equal("You are a Socratic tutor.", systemPrompt);
        // History the model sees ends with the just-appended user turn
        Assert.Equal(MessageRole.User, lastMessage!.Value.Role);
        Assert.Equal("why?", lastMessage.Value.Content);
    }

    // == Trimming == //

    [Fact]
    public async Task RunTurnAsync_Session_WhenHistoryExceedsWindow_TrimsToWholeTurnsAnchoredOnUser()
    {
        // Pre-seed two complete exchanges; window of 4 forces a trim once the new user turn lands.
        var session = new ProblemSession { Provider = AiProvider.Anthropic };
        session.Messages.AddRange(
        [
            new ChatMessage { Role = MessageRole.User,      Content = "U1" },
            new ChatMessage { Role = MessageRole.Assistant, Content = "A1" },
            new ChatMessage { Role = MessageRole.User,      Content = "U2" },
            new ChatMessage { Role = MessageRole.Assistant, Content = "A2" },
        ]);
        var store = StoreWith(session, session.SessionId);
        // Snapshot the window the model actually saw, before the assistant reply is appended back.
        List<string>? sentContents = null;
        MessageRole? firstRole = null;
        _llm.CompleteAsync(Arg.Do<CompletionRequest>(r =>
        {
            sentContents = r.Messages.Select(m => m.Content).ToList();
            firstRole    = r.Messages[0].Role;
        }), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "A3" });

        await _conversation.RunTurnAsync(store, session.SessionId, _ => Request(user: "U3", maxTurns: 4));

        // After appending U3 (count 5 > 4) the oldest message is dropped, leaving a leading Assistant (A1)
        // which is also dropped so the window stays anchored on a User message: [U2, A2, U3].
        Assert.Equal(MessageRole.User, firstRole);
        Assert.Equal(new[] { "U2", "A2", "U3" }, sentContents);
    }

    // == Failure Path == //

    [Fact]
    public async Task RunTurnAsync_Session_WhenLlmFails_RollsBackUserTurnAndDoesNotPersist()
    {
        var session = new ProblemSession { Provider = AiProvider.Anthropic };
        session.Messages.Add(new ChatMessage { Role = MessageRole.User,      Content = "earlier" });
        session.Messages.Add(new ChatMessage { Role = MessageRole.Assistant, Content = "reply" });
        var store = StoreWith(session, session.SessionId);
        _llm.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns<LlmResponse>(_ => throw new InvalidOperationException("provider down"));

        await Assert.ThrowsAsync<AiServiceException>(
            () => _conversation.RunTurnAsync(store, session.SessionId, _ => Request(user: "new")));

        Assert.Equal(2, session.Messages.Count);              // optimistic user turn rolled back
        Assert.Equal("reply", session.Messages[^1].Content);
        store.DidNotReceive().Set(Arg.Any<ProblemSession>()); // nothing persisted on failure
    }

    [Fact]
    public async Task RunTurnAsync_Session_WhenLlmThrowsAiServiceException_RethrowsWithoutDoubleWrapping()
    {
        var session  = new ProblemSession { Provider = AiProvider.Anthropic };
        var store    = StoreWith(session, session.SessionId);
        var original = new AiServiceException("upstream rate limited");
        _llm.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns<LlmResponse>(_ => throw original);

        var thrown = await Assert.ThrowsAsync<AiServiceException>(
            () => _conversation.RunTurnAsync(store, session.SessionId, _ => Request()));

        Assert.Same(original, thrown);
        Assert.Empty(session.Messages); // user turn still rolled back
    }

    [Fact]
    public async Task RunTurnAsync_Session_WhenCancelled_PropagatesCancellationAndRollsBack()
    {
        var session = new ProblemSession { Provider = AiProvider.Anthropic };
        var store   = StoreWith(session, session.SessionId);
        _llm.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns<LlmResponse>(_ => throw new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _conversation.RunTurnAsync(store, session.SessionId, _ => Request()));

        Assert.Empty(session.Messages); // cancellation must not leave a dangling user turn, and must not become a 502
    }

    [Fact]
    public async Task RunTurnAsync_Session_WhenLlmThrowsInsufficientQuota_RethrowsWithoutWrapping()
    {
        var session = new ProblemSession { Provider = AiProvider.Anthropic };
        session.Messages.Add(new ChatMessage { Role = MessageRole.User,      Content = "earlier" });
        session.Messages.Add(new ChatMessage { Role = MessageRole.Assistant, Content = "reply" });
        var store    = StoreWith(session, session.SessionId);
        var original = new InsufficientQuotaException("user-1", "Insufficient quota or credits for this request.");
        _llm.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns<LlmResponse>(_ => throw original);

        var thrown = await Assert.ThrowsAsync<InsufficientQuotaException>(
            () => _conversation.RunTurnAsync(store, session.SessionId, _ => Request(user: "new")));

        Assert.Same(original, thrown);
        Assert.Equal(2, session.Messages.Count); // optimistic user turn rolled back
        store.DidNotReceive().Set(Arg.Any<ProblemSession>());
    }

    // == Streaming Turn: same invariant, deltas pass through == //

    [Fact]
    public async Task RunTurnAsync_Session_WithOnDelta_StreamsTheReplyUnderTheSameInvariant()
    {
        var session = new ProblemSession { Provider = AiProvider.Anthropic };
        var store   = StoreWith(session, session.SessionId);
        _llm.StreamAsync(Arg.Any<CompletionRequest>(), Arg.Any<Func<string, CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var onDelta = callInfo.Arg<Func<string, CancellationToken, Task>>();
                await onDelta("What have ", CancellationToken.None);
                await onDelta("you tried?", CancellationToken.None);
                return new LlmResponse { Content = "What have you tried?" };
            });

        var deltas = new List<string>();
        var response = await _conversation.RunTurnAsync(store, session.SessionId, _ => Request(user: "I'm stuck"),
            (text, _) => { deltas.Add(text); return Task.CompletedTask; });

        Assert.Equal(["What have ", "you tried?"], deltas);
        Assert.Equal("What have you tried?", response.Content);
        Assert.Equal(2, session.Messages.Count);
        Assert.Equal(MessageRole.User,      session.Messages[0].Role);
        Assert.Equal(MessageRole.Assistant, session.Messages[1].Role);
        store.Received(1).Set(session);
    }

    [Fact]
    public async Task RunTurnAsync_Session_WhenStreamDiesMidReply_RollsBackUserTurnAndPersistsNothing()
    {
        // Deltas already reached the client, but history must never contain a partial assistant
        // message (providers reject malformed alternation) — the turn rolls back whole.
        var session = new ProblemSession { Provider = AiProvider.Anthropic };
        session.Messages.Add(new ChatMessage { Role = MessageRole.User,      Content = "earlier" });
        session.Messages.Add(new ChatMessage { Role = MessageRole.Assistant, Content = "reply" });
        var store = StoreWith(session, session.SessionId);

        async Task<LlmResponse> DieAfterOneDelta(NSubstitute.Core.CallInfo callInfo)
        {
            var onDelta = callInfo.Arg<Func<string, CancellationToken, Task>>();
            await onDelta("partial hint", CancellationToken.None);
            throw new InvalidOperationException("stream died");
        }

        _llm.StreamAsync(Arg.Any<CompletionRequest>(), Arg.Any<Func<string, CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(DieAfterOneDelta);

        await Assert.ThrowsAsync<AiServiceException>(
            () => _conversation.RunTurnAsync(store, session.SessionId, _ => Request(user: "new"),
                (_, _) => Task.CompletedTask));

        Assert.Equal(2, session.Messages.Count);              // optimistic user turn removed, no partial assistant turn
        Assert.Equal("reply", session.Messages[^1].Content);
        store.DidNotReceive().Set(Arg.Any<ProblemSession>());
    }
}
