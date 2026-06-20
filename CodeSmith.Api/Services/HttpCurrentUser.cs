// == Http Current User (dev bypass + Entra objectId extraction) == //
using CodeSmith.Core.Interfaces;
using CodeSmith.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace CodeSmith.Api.Services;

/// <summary>
/// Resolves the current user's Entra objectId from claims or debug header (only if explicitly allowed).
/// Also provides ClientIp for per-IP usage caps. Header bypass is locked down via AllowedDebugObjectIds.
/// </summary>
public class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;
    private readonly UsageOptions _options;

    public HttpCurrentUser(IHttpContextAccessor accessor, IOptions<UsageOptions> options)
    {
        _accessor = accessor;
        _options = options.Value;
    }

    public string? ObjectId
    {
        get
        {
            var ctx = _accessor.HttpContext;
            if (ctx is null) return null;

            var header = ctx.Request.Headers["X-Debug-User-Id"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(header))
            {
                // Only honor if explicitly listed (production list is empty)
                if (_options.AllowedDebugObjectIds != null &&
                    _options.AllowedDebugObjectIds.Contains(header, StringComparer.Ordinal))
                {
                    return header;
                }
            }

            // Entra External ID typical claims
            var user = ctx.User;
            if (user?.Identity?.IsAuthenticated != true) return null;

            return user.FindFirst("oid")?.Value
                ?? user.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
                ?? user.FindFirst("sub")?.Value; // fallback
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
