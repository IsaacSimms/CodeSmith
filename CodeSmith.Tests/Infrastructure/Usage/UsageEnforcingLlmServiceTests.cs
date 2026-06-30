// == Usage Enforcing Decorator Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Core.Models.Usage;
using CodeSmith.Infrastructure.Services.Usage.Decorators;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CodeSmith.Tests.Infrastructure.Usage;

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
}
