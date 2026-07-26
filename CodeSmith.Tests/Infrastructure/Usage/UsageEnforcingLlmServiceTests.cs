// == Usage Enforcing Decorator Tests == //
using System.Diagnostics;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Core.Models.Usage;
using CodeSmith.Infrastructure.Services.Usage.Decorators;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CodeSmith.Tests.Infrastructure.Usage;

[Collection("CodeSmithTelemetry")] // span capture is process-global — see ActivityCapture
public class UsageEnforcingLlmServiceTests
{
    private const string ObjectId = "user-1";
    private const string ClientIp = "1.2.3.4";
    private const AiProvider Provider = AiProvider.Anthropic;

    private static UsageReservation SampleReservation() => new()
    {
        ObjectId           = ObjectId,
        ClientIp           = ClientIp,
        Provider           = Provider,
        ReservedFreeTokens = 200,
        ReservedPaidUsd    = 0m
    };

    private static (UsageEnforcingLlmService Sut, ILlmService Inner, IUsageEnforcer Enforcer) Build(UsageReservation reservation)
    {
        var inner       = Substitute.For<ILlmService>();
        var enforcer    = Substitute.For<IUsageEnforcer>();
        var pricing     = Substitute.For<ILlmPricing>();
        var currentUser = Substitute.For<ICurrentUser>();

        currentUser.ObjectId.Returns(ObjectId);
        currentUser.ClientIp.Returns(ClientIp);
        enforcer.ReserveAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AiProvider>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(reservation);

        var sut = new UsageEnforcingLlmService(inner, currentUser, enforcer, pricing, Provider);
        return (sut, inner, enforcer);
    }

    private static CompletionRequest Request()
        => CompletionRequest.SingleTurn("system", "hello", ModelTier.Fast, 256, "Tutoring:Guidance");

    [Fact]
    public async Task CompleteAsync_OnSuccess_ReservesThenSettles_AndDoesNotRelease()
    {
        var reservation = SampleReservation();
        var (sut, inner, enforcer) = Build(reservation);

        inner.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "hi", Model = "claude-haiku-4-5", InputTokensUsed = 5, OutputTokensUsed = 3 });

        var response = await sut.CompleteAsync(Request());

        Assert.Equal("hi", response.Content);
        await enforcer.Received(1).SettleAsync(reservation, "claude-haiku-4-5", 5, 3, Arg.Any<decimal>(), Arg.Any<decimal>(), "Tutoring:Guidance", Arg.Any<CancellationToken>());
        await enforcer.DidNotReceive().ReleaseAsync(Arg.Any<UsageReservation>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_WhenInnerCallFails_ReleasesReservation_AndDoesNotSettle()
    {
        var reservation = SampleReservation();
        var (sut, inner, enforcer) = Build(reservation);

        inner.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("provider exploded"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CompleteAsync(Request()));

        await enforcer.Received(1).ReleaseAsync(reservation, Arg.Any<CancellationToken>());
        await enforcer.DidNotReceive().SettleAsync(
            Arg.Any<UsageReservation>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // == Telemetry Span Tests == //

    [Fact]
    public async Task CompleteAsync_OnSuccess_EmitsCompletionSpanWithPhaseChildren()
    {
        using var capture = new ActivityCapture();
        var (sut, inner, _) = Build(SampleReservation());

        inner.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "hi", Model = "claude-haiku-4-5", InputTokensUsed = 5, OutputTokensUsed = 3 });

        await sut.CompleteAsync(Request());

        // Root span carries the caller intent
        var completion = capture.Single("llm.completion");
        Assert.Equal("Anthropic",         completion.GetTagItem("codesmith.provider")?.ToString());
        Assert.Equal("Fast",              completion.GetTagItem("codesmith.tier")?.ToString());
        Assert.Equal("Tutoring:Guidance", completion.GetTagItem("codesmith.feature")?.ToString());

        // Each lifecycle phase is a child span so provider time vs enforcement time is separable
        var call = capture.Single("llm.call");
        Assert.Equal(completion.SpanId, call.ParentSpanId);
        Assert.Equal("claude-haiku-4-5", call.GetTagItem("codesmith.model")?.ToString());
        Assert.Equal(5, call.GetTagItem("codesmith.tokens.input"));
        Assert.Equal(3, call.GetTagItem("codesmith.tokens.output"));

        Assert.Equal(completion.SpanId, capture.Single("usage.reserve").ParentSpanId);
        Assert.Equal(completion.SpanId, capture.Single("usage.settle").ParentSpanId);
        Assert.Empty(capture.All("usage.release"));
    }

    [Fact]
    public async Task CompleteAsync_WhenInnerCallFails_EmitsReleaseSpanAndErrorStatus()
    {
        using var capture = new ActivityCapture();
        var (sut, inner, _) = Build(SampleReservation());

        inner.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("provider exploded"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CompleteAsync(Request()));

        Assert.Equal(ActivityStatusCode.Error, capture.Single("llm.completion").Status);
        Assert.Single(capture.All("usage.release"));
        Assert.Empty(capture.All("usage.settle"));
    }

    // == Streaming: same lifecycle, deltas pass through, settle on final actuals == //

    // Wires the inner fake to emit the given deltas through the caller's onDelta, then return the response
    private static void InnerStreams(ILlmService inner, LlmResponse response, params string[] deltas)
    {
        inner.StreamAsync(Arg.Any<CompletionRequest>(), Arg.Any<Func<string, CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var onDelta = callInfo.Arg<Func<string, CancellationToken, Task>>();
                foreach (var delta in deltas)
                    await onDelta(delta, CancellationToken.None);
                return response;
            });
    }

    [Fact]
    public async Task StreamAsync_OnSuccess_PassesDeltasThroughAndSettlesOnFinalActuals()
    {
        var reservation = SampleReservation();
        var (sut, inner, enforcer) = Build(reservation);
        InnerStreams(inner, new LlmResponse { Content = "Hello!", Model = "claude-haiku-4-5", InputTokensUsed = 5, OutputTokensUsed = 3 }, "Hel", "lo!");

        var deltas = new List<string>();
        var response = await sut.StreamAsync(Request(), (text, _) =>
        {
            deltas.Add(text);
            return Task.CompletedTask;
        });

        Assert.Equal(["Hel", "lo!"], deltas);
        Assert.Equal("Hello!", response.Content);
        await enforcer.Received(1).SettleAsync(reservation, "claude-haiku-4-5", 5, 3, Arg.Any<decimal>(), Arg.Any<decimal>(), "Tutoring:Guidance", Arg.Any<CancellationToken>());
        await enforcer.DidNotReceive().ReleaseAsync(Arg.Any<UsageReservation>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StreamAsync_WhenStreamDiesMidReply_ReleasesReservation_AndDoesNotSettle()
    {
        // Deltas were produced but the stream died before final usage arrived: the hold is refunded
        // (user pays nothing for an undelivered turn) — the locked mid-stream billing decision.
        var reservation = SampleReservation();
        var (sut, inner, enforcer) = Build(reservation);

        // Explicitly typed so the throwing async lambda still infers Task<LlmResponse> for NSubstitute
        async Task<LlmResponse> DieAfterOneDelta(NSubstitute.Core.CallInfo callInfo)
        {
            var onDelta = callInfo.Arg<Func<string, CancellationToken, Task>>();
            await onDelta("partial", CancellationToken.None);
            throw new InvalidOperationException("stream died");
        }

        inner.StreamAsync(Arg.Any<CompletionRequest>(), Arg.Any<Func<string, CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(DieAfterOneDelta);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.StreamAsync(Request(), (_, _) => Task.CompletedTask));

        await enforcer.Received(1).ReleaseAsync(reservation, Arg.Any<CancellationToken>());
        await enforcer.DidNotReceive().SettleAsync(
            Arg.Any<UsageReservation>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StreamAsync_EmitsPhaseSpans_WithTimeToFirstTokenOnCall()
    {
        using var capture = new ActivityCapture();
        var (sut, inner, _) = Build(SampleReservation());
        InnerStreams(inner, new LlmResponse { Content = "hi", Model = "claude-haiku-4-5", InputTokensUsed = 5, OutputTokensUsed = 3 }, "hi");

        await sut.StreamAsync(Request(), (_, _) => Task.CompletedTask);

        var completion = capture.Single("llm.completion");
        var call       = capture.Single("llm.call");
        Assert.Equal(completion.SpanId, call.ParentSpanId);
        Assert.Equal(completion.SpanId, capture.Single("usage.reserve").ParentSpanId);
        Assert.Equal(completion.SpanId, capture.Single("usage.settle").ParentSpanId);

        // The instrumentation was built partly for this: streaming stamps time-to-first-token
        var ttft = call.GetTagItem("codesmith.time_to_first_token_ms");
        Assert.NotNull(ttft);
        Assert.True(Convert.ToInt64(ttft) >= 0);
    }
}
