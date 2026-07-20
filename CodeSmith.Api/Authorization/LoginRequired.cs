// == Login-required 401 contract (metered AI) == //
namespace CodeSmith.Api.Authorization;

/// <summary>
/// Fixed ProblemDetails fields returned when an unauthenticated (or otherwise unauthorized)
/// caller hits a <see cref="MeteredAiAttribute"/> endpoint.
/// </summary>
public static class LoginRequired
{
    public const string Title  = "Login required";
    public const string Detail = "Sign in with an account to use tokens.";
    public const string Code   = "login_required";
}
