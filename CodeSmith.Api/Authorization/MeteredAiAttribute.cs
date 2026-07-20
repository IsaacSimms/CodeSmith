// == Metered AI Endpoint Marker (auth + login_required 401) == //
using Microsoft.AspNetCore.Authorization;

namespace CodeSmith.Api.Authorization;

/// <summary>
/// Marks an LLM-metered action: requires an authenticated account and, on auth failure,
/// selects the login_required ProblemDetails body (see
/// <see cref="MeteredAiAuthorizationMiddlewareResultHandler"/>). Subclasses
/// <see cref="AuthorizeAttribute"/> so auth cannot be forgotten independently of the marker.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class MeteredAiAttribute : AuthorizeAttribute
{
}
