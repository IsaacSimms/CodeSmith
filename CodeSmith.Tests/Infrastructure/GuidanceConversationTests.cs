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

    private static GuidanceTurnRequest Request(string user = "help me", int maxTokens = 1024, int maxTurns = 20, string feature = "Tutoring:Guidance")
        => new()
        {
            SystemPrompt = "You are a Socratic tutor.",
            UserMessage  = user,
            MaxTokens    = maxTokens,
            MaxTurns     = maxTurns,
            Feature      = feature
        };

    // == Success Path == //

    [Fact]
    public async Task RunTurnAsync_OnSuccess_AppendsUserThenAssistantToHistory()
    {
        var history = new List<ChatMessage>();
        _llm.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "What have you tried?" });

        await _conversation.RunTurnAsync(AiProvider.Anthropic, history, Request(user: "I'm stuck"), () => { });

        Assert.Equal(2, history.Count);
        Assert.Equal(MessageRole.User, history[0].Role);
        Assert.Equal("I'm stuck", history[0].Content);
        Assert.Equal(MessageRole.Assistant, history[1].Role);
        Assert.Equal("What have you tried?", history[1].Content);
    }

    [Fact]
    public async Task RunTurnAsync_OnSuccess_ReturnsCompletionAndPersistsOnce()
    {
        var history    = new List<ChatMessage>();
        var persistHits = 0;
        var expected   = new LlmResponse { Content = "Consider the edge case.", InputTokensUsed = 42, ContextWindowSize = 200_000 };
        _llm.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>()).Returns(expected);

        var response = await _conversation.RunTurnAsync(AiProvider.Anthropic, history, Request(), () => persistHits++);

        Assert.Same(expected, response);
        Assert.Equal(1, persistHits);
    }

    [Fact]
    public async Task RunTurnAsync_SendsFastTierWithRequestFeatureMaxTokensAndCurrentHistory()
    {
        var history = new List<ChatMessage>();
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

        await _conversation.RunTurnAsync(
            AiProvider.Anthropic, history, Request(user: "why?", maxTokens: 800, feature: "SystemLab:Chat"), () => { });

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
    public async Task RunTurnAsync_WhenHistoryExceedsWindow_TrimsToWholeTurnsAnchoredOnUser()
    {
        // Pre-seed two complete exchanges; window of 4 forces a trim once the new user turn lands.
        var history = new List<ChatMessage>
        {
            new() { Role = MessageRole.User,      Content = "U1" },
            new() { Role = MessageRole.Assistant, Content = "A1" },
            new() { Role = MessageRole.User,      Content = "U2" },
            new() { Role = MessageRole.Assistant, Content = "A2" },
        };
        // Snapshot the window the model actually saw, before the assistant reply is appended back.
        List<string>? sentContents = null;
        MessageRole? firstRole = null;
        _llm.CompleteAsync(Arg.Do<CompletionRequest>(r =>
        {
            sentContents = r.Messages.Select(m => m.Content).ToList();
            firstRole    = r.Messages[0].Role;
        }), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "A3" });

        await _conversation.RunTurnAsync(AiProvider.Anthropic, history, Request(user: "U3", maxTurns: 4), () => { });

        // After appending U3 (count 5 > 4) the oldest message is dropped, leaving a leading Assistant (A1)
        // which is also dropped so the window stays anchored on a User message: [U2, A2, U3].
        Assert.Equal(MessageRole.User, firstRole);
        Assert.Equal(new[] { "U2", "A2", "U3" }, sentContents);
    }

    // == Failure Path == //

    [Fact]
    public async Task RunTurnAsync_WhenLlmFails_RollsBackUserTurnAndDoesNotPersist()
    {
        var history = new List<ChatMessage>
        {
            new() { Role = MessageRole.User,      Content = "earlier" },
            new() { Role = MessageRole.Assistant, Content = "reply" },
        };
        var persistHits = 0;
        _llm.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns<LlmResponse>(_ => throw new InvalidOperationException("provider down"));

        await Assert.ThrowsAsync<AiServiceException>(
            () => _conversation.RunTurnAsync(AiProvider.Anthropic, history, Request(user: "new"), () => persistHits++));

        Assert.Equal(2, history.Count);                       // optimistic user turn removed
        Assert.Equal("reply", history[^1].Content);
        Assert.Equal(0, persistHits);                         // nothing persisted on failure
    }

    [Fact]
    public async Task RunTurnAsync_WhenLlmThrowsAiServiceException_RethrowsWithoutDoubleWrapping()
    {
        var history = new List<ChatMessage>();
        var original = new AiServiceException("upstream rate limited");
        _llm.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns<LlmResponse>(_ => throw original);

        var thrown = await Assert.ThrowsAsync<AiServiceException>(
            () => _conversation.RunTurnAsync(AiProvider.Anthropic, history, Request(), () => { }));

        Assert.Same(original, thrown);
        Assert.Empty(history); // user turn still rolled back
    }

    [Fact]
    public async Task RunTurnAsync_WhenCancelled_PropagatesCancellationAndRollsBack()
    {
        var history = new List<ChatMessage>();
        _llm.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns<LlmResponse>(_ => throw new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _conversation.RunTurnAsync(AiProvider.Anthropic, history, Request(), () => { }));

        Assert.Empty(history); // cancellation must not leave a dangling user turn, and must not become a 502
    }

    [Fact]
    public async Task RunTurnAsync_WhenLlmThrowsInsufficientQuota_RethrowsWithoutWrapping()
    {
        var history = new List<ChatMessage>
        {
            new() { Role = MessageRole.User,      Content = "earlier" },
            new() { Role = MessageRole.Assistant, Content = "reply" },
        };
        var persistHits = 0;
        var original = new InsufficientQuotaException("user-1", "Insufficient quota or credits for this request.");
        _llm.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns<LlmResponse>(_ => throw original);

        var thrown = await Assert.ThrowsAsync<InsufficientQuotaException>(
            () => _conversation.RunTurnAsync(AiProvider.Anthropic, history, Request(user: "new"), () => persistHits++));

        Assert.Same(original, thrown);
        Assert.Equal(2, history.Count); // optimistic user turn rolled back
        Assert.Equal(0, persistHits);
    }

    // == Streaming Turn: same invariant, deltas pass through == //

    [Fact]
    public async Task StreamTurnAsync_OnSuccess_DeliversDeltasAppendsBothTurnsAndPersistsOnce()
    {
        var history     = new List<ChatMessage>();
        var persistHits = 0;
        _llm.StreamAsync(Arg.Any<CompletionRequest>(), Arg.Any<Func<string, CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var onDelta = callInfo.Arg<Func<string, CancellationToken, Task>>();
                await onDelta("What have ", CancellationToken.None);
                await onDelta("you tried?", CancellationToken.None);
                return new LlmResponse { Content = "What have you tried?" };
            });

        var deltas = new List<string>();
        var response = await _conversation.StreamTurnAsync(
            AiProvider.Anthropic, history, Request(user: "I'm stuck"),
            (text, _) => { deltas.Add(text); return Task.CompletedTask; },
            () => persistHits++);

        Assert.Equal(["What have ", "you tried?"], deltas);
        Assert.Equal("What have you tried?", response.Content);
        Assert.Equal(1, persistHits);
        Assert.Equal(2, history.Count);
        Assert.Equal(MessageRole.User, history[0].Role);
        Assert.Equal("I'm stuck", history[0].Content);
        Assert.Equal(MessageRole.Assistant, history[1].Role);
        Assert.Equal("What have you tried?", history[1].Content);
    }

    [Fact]
    public async Task StreamTurnAsync_WhenStreamDiesMidReply_RollsBackUserTurnAndPersistsNothing()
    {
        // Deltas already reached the client, but history must never contain a partial assistant
        // message (providers reject malformed alternation) — the turn rolls back whole.
        var history = new List<ChatMessage>
        {
            new() { Role = MessageRole.User,      Content = "earlier" },
            new() { Role = MessageRole.Assistant, Content = "reply" },
        };
        var persistHits = 0;

        async Task<LlmResponse> DieAfterOneDelta(NSubstitute.Core.CallInfo callInfo)
        {
            var onDelta = callInfo.Arg<Func<string, CancellationToken, Task>>();
            await onDelta("partial hint", CancellationToken.None);
            throw new InvalidOperationException("stream died");
        }

        _llm.StreamAsync(Arg.Any<CompletionRequest>(), Arg.Any<Func<string, CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(DieAfterOneDelta);

        await Assert.ThrowsAsync<AiServiceException>(
            () => _conversation.StreamTurnAsync(AiProvider.Anthropic, history, Request(user: "new"),
                (_, _) => Task.CompletedTask, () => persistHits++));

        Assert.Equal(2, history.Count);       // optimistic user turn removed, no partial assistant turn
        Assert.Equal("reply", history[^1].Content);
        Assert.Equal(0, persistHits);
    }

    [Fact]
    public async Task StreamTurnAsync_WhenCancelled_PropagatesCancellationAndRollsBack()
    {
        var history = new List<ChatMessage>();
        _llm.StreamAsync(Arg.Any<CompletionRequest>(), Arg.Any<Func<string, CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns<LlmResponse>(_ => throw new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _conversation.StreamTurnAsync(AiProvider.Anthropic, history, Request(),
                (_, _) => Task.CompletedTask, () => { }));

        Assert.Empty(history); // cancellation must not leave a dangling user turn, and must not become a 502
    }
}
