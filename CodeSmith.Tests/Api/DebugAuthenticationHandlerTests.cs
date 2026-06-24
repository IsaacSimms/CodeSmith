// == Debug Authentication Handler Tests == //

using System.Text.Encodings.Web;
using CodeSmith.Api.Services;
using CodeSmith.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CodeSmith.Tests.Api;

public class DebugAuthenticationHandlerTests
{
    private static DebugAuthenticationHandler CreateHandler(string[] allowedIds)
    {
        var schemeOptions = new AuthenticationSchemeOptions();
        var optionsMonitor = Substitute.For<IOptionsMonitor<AuthenticationSchemeOptions>>();
        optionsMonitor.Get("Debug").Returns(schemeOptions);

        var logger = Substitute.For<ILogger<DebugAuthenticationHandler>>();
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(logger);

        var encoder = UrlEncoder.Default;

        var usageOptions = Options.Create(new UsageOptions
        {
            AllowedDebugObjectIds = allowedIds
        });

        return new DebugAuthenticationHandler(optionsMonitor, loggerFactory, encoder, usageOptions);
    }

    private static async Task<AuthenticateResult> RunAsync(DebugAuthenticationHandler handler, string? headerValue)
    {
        var context = new DefaultHttpContext();
        if (headerValue is not null)
        {
            context.Request.Headers["X-Debug-User-Id"] = headerValue;
        }

        var scheme = new AuthenticationScheme("Debug", displayName: null, handlerType: typeof(DebugAuthenticationHandler));
        await handler.InitializeAsync(scheme, context);
        return await handler.AuthenticateAsync();
    }

    // == Success Path == //

    [Fact]
    public async Task Authenticate_WithListedHeader_ReturnsSuccessWithOidClaim()
    {
        var handler = CreateHandler(new[] { "11111111-1111-1111-1111-111111111111", "allowed-user" });

        var result = await RunAsync(handler, "11111111-1111-1111-1111-111111111111");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Principal);
        Assert.True(result.Principal.Identity?.IsAuthenticated);
        Assert.Equal("11111111-1111-1111-1111-111111111111", result.Principal.FindFirst("oid")?.Value);
        Assert.Equal("11111111-1111-1111-1111-111111111111", result.Principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("Debug", result.Ticket?.AuthenticationScheme);
    }

    [Fact]
    public async Task Authenticate_WithListedHeader_SetsAuthenticationTypeFromScheme()
    {
        var handler = CreateHandler(new[] { "my-test-user-123" });

        var result = await RunAsync(handler, "my-test-user-123");

        Assert.True(result.Succeeded);
        var identity = result.Principal?.Identity as System.Security.Claims.ClaimsIdentity;
        Assert.NotNull(identity);
        Assert.Equal("Debug", identity.AuthenticationType);
    }

    // == No Result Paths == //

    [Fact]
    public async Task Authenticate_MissingHeader_ReturnsNoResult()
    {
        var handler = CreateHandler(new[] { "11111111-1111-1111-1111-111111111111" });

        var result = await RunAsync(handler, null);

        Assert.False(result.Succeeded);
        Assert.True(result.None);
        Assert.Null(result.Principal);
    }

    [Fact]
    public async Task Authenticate_UnlistedHeader_ReturnsNoResult()
    {
        var handler = CreateHandler(new[] { "11111111-1111-1111-1111-111111111111" });

        var result = await RunAsync(handler, "unlisted-guid-9999-9999-9999-999999999999");

        Assert.True(result.None);
        Assert.False(result.Succeeded);
        Assert.Null(result.Principal);
    }

    [Fact]
    public async Task Authenticate_EmptyHeader_ReturnsNoResult()
    {
        var handler = CreateHandler(new[] { "11111111-1111-1111-1111-111111111111" });

        var result = await RunAsync(handler, "   ");

        Assert.True(result.None);
    }

    [Fact]
    public async Task Authenticate_WhenNoAllowedIdsConfigured_NeverSucceeds()
    {
        var handler = CreateHandler(Array.Empty<string>());

        var result = await RunAsync(handler, "11111111-1111-1111-1111-111111111111");

        Assert.True(result.None);
    }
}
