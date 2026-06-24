// == Debug Authentication Handler == //

using System.Security.Claims;
using System.Text.Encodings.Web;
using CodeSmith.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeSmith.Api.Services;

/// <summary>
/// Development-only authentication handler. When a request carries an X-Debug-User-Id header
/// whose value is listed in UsageOptions.AllowedDebugObjectIds, it produces a successful
/// AuthenticationTicket with the minimal claims needed for [Authorize] and for HttpCurrentUser
/// to resolve the objectId. All other cases (missing header or unlisted value) return NoResult
/// so that the authorization pipeline correctly denies.
/// 
/// This satisfies [Authorize] on spending endpoints without altering any usage enforcement,
/// decorators, HttpCurrentUser logic, or controller attributes. It is a temporary bridge until
/// full Entra External ID is wired.
/// </summary>
public class DebugAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly UsageOptions _usageOptions;

    public DebugAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<UsageOptions> usageOptions)
        : base(options, logger, encoder)
    {
        _usageOptions = usageOptions.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headerValue = Request.Headers["X-Debug-User-Id"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!IsAllowedDebugObjectId(headerValue))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim("oid", headerValue),
            new Claim(ClaimTypes.NameIdentifier, headerValue)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private bool IsAllowedDebugObjectId(string value)
    {
        var allowed = _usageOptions.AllowedDebugObjectIds;
        if (allowed == null || allowed.Length == 0)
        {
            return false;
        }

        return allowed.Contains(value, StringComparer.Ordinal);
    }
}
