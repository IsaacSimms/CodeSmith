// == Metered AI Authorization Result Handler == //
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace CodeSmith.Api.Authorization;

/// <summary>
/// On authorization failure for endpoints marked <see cref="MeteredAiAttribute"/>, writes a fixed
/// 401 ProblemDetails (login_required) instead of the stock challenge body. All other endpoints
/// use the default ASP.NET Core authorization result handler.
/// </summary>
public sealed class MeteredAiAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();
    private readonly IProblemDetailsService _problemDetails;

    public MeteredAiAuthorizationMiddlewareResultHandler(IProblemDetailsService problemDetails)
    {
        _problemDetails = problemDetails;
    }

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
        {
            await _default.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        var isMeteredAi = context.GetEndpoint()?.Metadata.GetMetadata<MeteredAiAttribute>() is not null;
        if (!isMeteredAi)
        {
            await _default.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        // == login_required body (do not call next; do not run default challenge write) == //
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title  = LoginRequired.Title,
            Detail = LoginRequired.Detail,
        };
        problem.Extensions["code"] = LoginRequired.Code;

        await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext    = context,
            ProblemDetails = problem,
        });
    }
}
