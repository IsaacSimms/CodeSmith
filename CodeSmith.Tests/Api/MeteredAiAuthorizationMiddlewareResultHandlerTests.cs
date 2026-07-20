// == Metered AI Authorization Result Handler Tests == //
using CodeSmith.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using NSubstitute;

namespace CodeSmith.Tests.Api;

/// <summary>
/// Pins the login_required 401 contract for metered AI endpoints and the default path for
/// non-metered authorized endpoints (billing-style). Handler is unit-tested with endpoint metadata.
/// </summary>
public class MeteredAiAuthorizationMiddlewareResultHandlerTests
{
    private readonly IProblemDetailsService _problemDetails = Substitute.For<IProblemDetailsService>();

    private MeteredAiAuthorizationMiddlewareResultHandler CreateSut()
        => new(_problemDetails);

    private static DefaultHttpContext MakeContext(bool meteredAi)
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();

        var metadata = new EndpointMetadataCollection(
            meteredAi ? [new MeteredAiAttribute()] : Array.Empty<object>());

        var endpoint = new RouteEndpoint(
            requestDelegate: _ => Task.CompletedTask,
            routePattern: RoutePatternFactory.Parse("/test"),
            order: 0,
            metadata: metadata,
            displayName: "test");

        ctx.SetEndpoint(endpoint);
        return ctx;
    }

    private static PolicyAuthorizationResult FailedAuth()
        => PolicyAuthorizationResult.Challenge();

    private static PolicyAuthorizationResult SucceededAuth()
        => PolicyAuthorizationResult.Success();

    private static AuthorizationPolicy AnyPolicy()
        => new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

    // == Metered AI: failed auth → login_required body == //

    [Fact]
    public async Task HandleAsync_MeteredAi_FailedAuth_WritesLoginRequiredProblemDetails()
    {
        ProblemDetailsContext? captured = null;
        _problemDetails.TryWriteAsync(Arg.Do<ProblemDetailsContext>(c => captured = c)).Returns(true);

        var context = MakeContext(meteredAi: true);
        var nextCalled = false;

        await CreateSut().HandleAsync(
            _ => { nextCalled = true; return Task.CompletedTask; },
            context,
            AnyPolicy(),
            FailedAuth());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.NotNull(captured);
        Assert.Equal(LoginRequired.Title, captured.ProblemDetails.Title);
        Assert.Equal(LoginRequired.Detail, captured.ProblemDetails.Detail);
        Assert.Equal(StatusCodes.Status401Unauthorized, captured.ProblemDetails.Status);
        Assert.True(captured.ProblemDetails.Extensions.TryGetValue("code", out var code));
        Assert.Equal(LoginRequired.Code, code as string);
    }

    [Fact]
    public async Task HandleAsync_MeteredAi_FailedAuth_DoesNotCallNext()
    {
        _problemDetails.TryWriteAsync(Arg.Any<ProblemDetailsContext>()).Returns(true);

        var nextCalled = false;
        await CreateSut().HandleAsync(
            _ => { nextCalled = true; return Task.CompletedTask; },
            MakeContext(meteredAi: true),
            AnyPolicy(),
            FailedAuth());

        Assert.False(nextCalled);
    }

    // == Non-metered: failed auth → no login_required write from our path == //

    [Fact]
    public async Task HandleAsync_NonMetered_FailedAuth_DoesNotWriteLoginRequired()
    {
        var context = MakeContext(meteredAi: false);

        // Default handler will try to challenge; no auth service is configured on this context,
        // so we only assert our ProblemDetails path was not used for login_required.
        try
        {
            await CreateSut().HandleAsync(
                _ => Task.CompletedTask,
                context,
                AnyPolicy(),
                FailedAuth());
        }
        catch
        {
            // Default challenge may throw without a full auth stack; that is fine for this unit test.
        }

        await _problemDetails.DidNotReceive().TryWriteAsync(Arg.Any<ProblemDetailsContext>());
    }

    // == Succeeded auth: always continue via default (next runs) == //

    [Fact]
    public async Task HandleAsync_Succeeded_CallsNext()
    {
        var nextCalled = false;
        await CreateSut().HandleAsync(
            _ => { nextCalled = true; return Task.CompletedTask; },
            MakeContext(meteredAi: true),
            AnyPolicy(),
            SucceededAuth());

        Assert.True(nextCalled);
        await _problemDetails.DidNotReceive().TryWriteAsync(Arg.Any<ProblemDetailsContext>());
    }
}
