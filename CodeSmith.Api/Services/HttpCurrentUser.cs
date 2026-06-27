// == Http Current User (Entra objectId from authenticated claims) == //
using CodeSmith.Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace CodeSmith.Api.Services;

/// <summary>
/// Resolves the current user's Entra objectId from authenticated claims.
/// Also provides ClientIp for per-IP usage caps.
/// </summary>
public class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public string? ObjectId
    {
        get
        {
            var ctx = _accessor.HttpContext;
            if (ctx is null) return null;

            var user = ctx.User;
            if (user?.Identity?.IsAuthenticated != true) return null;

            return user.FindFirst("oid")?.Value
                ?? user.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
                ?? user.FindFirst("sub")?.Value;
        }
    }

    public string? ClientIp
    {
        get
        {
            var ctx = _accessor.HttpContext;
            if (ctx is null) return null;

            // After ForwardedHeaders middleware, this is the real client IP
            var ip = ctx.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrWhiteSpace(ip)) return "unknown";

            return ip;
        }
    }
}