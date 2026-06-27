// == Http Current User Tests == //

using System.Net;
using System.Security.Claims;
using CodeSmith.Api.Services;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace CodeSmith.Tests.Api;

public class HttpCurrentUserTests
{
    private static HttpCurrentUser CreateSut(DefaultHttpContext context)
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);
        return new HttpCurrentUser(accessor);
    }

    private static void SetAuthenticatedUser(DefaultHttpContext context, params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, authenticationType: "Bearer");
        context.User = new ClaimsPrincipal(identity);
    }

    [Fact]
    public void ObjectId_WhenAuthenticatedWithOidClaim_ReturnsOid()
    {
        var context = new DefaultHttpContext();
        SetAuthenticatedUser(context, new Claim("oid", "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));

        var sut = CreateSut(context);

        Assert.Equal("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", sut.ObjectId);
    }

    [Fact]
    public void ObjectId_WhenAuthenticatedWithObjectIdentifierClaim_ReturnsValue()
    {
        var context = new DefaultHttpContext();
        SetAuthenticatedUser(context,
            new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", "bbbbbbbb-cccc-dddd-eeee-ffffffffffff"));

        var sut = CreateSut(context);

        Assert.Equal("bbbbbbbb-cccc-dddd-eeee-ffffffffffff", sut.ObjectId);
    }

    [Fact]
    public void ObjectId_WhenAuthenticatedWithSubOnly_ReturnsSub()
    {
        var context = new DefaultHttpContext();
        SetAuthenticatedUser(context, new Claim("sub", "subject-only-id"));

        var sut = CreateSut(context);

        Assert.Equal("subject-only-id", sut.ObjectId);
    }

    [Fact]
    public void ObjectId_WhenNotAuthenticated_ReturnsNull()
    {
        var context = new DefaultHttpContext();
        var sut = CreateSut(context);

        Assert.Null(sut.ObjectId);
    }

    [Fact]
    public void ObjectId_WhenNotAuthenticated_IgnoresDebugHeader()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Debug-User-Id"] = "11111111-1111-1111-1111-111111111111";

        var sut = CreateSut(context);

        Assert.Null(sut.ObjectId);
    }

    [Fact]
    public void ObjectId_WhenAuthenticated_PrefersOidOverSub()
    {
        var context = new DefaultHttpContext();
        SetAuthenticatedUser(context,
            new Claim("oid", "oid-wins"),
            new Claim("sub", "sub-loses"));

        var sut = CreateSut(context);

        Assert.Equal("oid-wins", sut.ObjectId);
    }

    [Fact]
    public void ObjectId_WhenHttpContextMissing_ReturnsNull()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        var sut = new HttpCurrentUser(accessor);

        Assert.Null(sut.ObjectId);
    }

    [Fact]
    public void ClientIp_ReturnsRemoteIpAddress()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");

        var sut = CreateSut(context);

        Assert.Equal("203.0.113.10", sut.ClientIp);
    }

    [Fact]
    public void ClientIp_WhenMissing_ReturnsUnknown()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = null;

        var sut = CreateSut(context);

        Assert.Equal("unknown", sut.ClientIp);
    }
}