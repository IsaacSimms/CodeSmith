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
            .Returns(new LlmResponse { Content = "hi", Model = "claude-haiku-4-5-20251001", InputTokensUsed = 5, OutputTokensUsed = 3 });

        var response = await sut.CompleteAsync(Request());

        Assert.Equal("hi", response.Content);
        await enforcer.Received(1).SettleAsync(reservation, "claude-haiku-4-5-20251001", 5, 3, Arg.Any<decimal>(), Arg.Any<decimal>(), "Tutoring:Guidance", Arg.Any<CancellationToken>());
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
            .Returns(new LlmResponse { Content = "hi", Model = "claude-haiku-4-5-20251001", InputTokensUsed = 5, OutputTokensUsed = 3 });

        await sut.CompleteAsync(Request());

        // Root span carries the caller intent
        var completion = capture.Single("llm.completion");
        Assert.Equal("Anthropic",         completion.GetTagItem("codesmith.provider")?.ToString());
        Assert.Equal("Fast",              completion.GetTagItem("codesmith.tier")?.ToString());
        Assert.Equal("Tutoring:Guidance", completion.GetTagItem("codesmith.feature")?.ToString());

        // Each lifecycle phase is a child span so provider time vs enforcement time is separable
        var call = capture.Single("llm.call");
        Assert.Equal(completion.SpanId, call.ParentSpanId);
        Assert.Equal("claude-haiku-4-5-20251001", call.GetTagItem("codesmith.model")?.ToString());
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
}
