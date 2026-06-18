// == Http Current User (dev bypass + Entra objectId extraction) == //
using CodeSmith.Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace CodeSmith.Api.Services;

/// <summary>
/// Resolves the current user's Entra objectId from claims or dev bypass header.
/// This is the only place that knows how to extract the stable user id.
/// </summary>
public class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;
    private readonly IHostEnvironment _env;

    public HttpCurrentUser(IHttpContextAccessor accessor, IHostEnvironment env)
    {
        _accessor = accessor;
        _env = env;
    }

    public string? ObjectId
    {
        get
        {
            var ctx = _accessor.HttpContext;
            if (ctx is null) return null;

            // Dev bypass header (works for local tests and against CA with manual token/header)
            if (_env.IsDevelopment() || ctx.Request.Headers.ContainsKey("X-Debug-User-Id"))
            {
                var header = ctx.Request.Headers["X-Debug-User-Id"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(header))
                    return header;
            }

            // Entra External ID typical claims
            var user = ctx.User;
            if (user?.Identity?.IsAuthenticated != true) return null;

            return user.FindFirst("oid")?.Value
                ?? user.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
                ?? user.FindFirst("sub")?.Value; // fallback
        }
    }
}
