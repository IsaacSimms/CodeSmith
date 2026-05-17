// == App Exception Handler == //
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CodeSmith.Api.Middleware;

/// <summary>
/// Iterates registered IExceptionMapper adapters to produce RFC 7807 ProblemDetails responses.
/// Falls back to 500 when no mapper claims the exception. Adding new exception types
/// requires only a new IExceptionMapper registration — this class never changes.
/// </summary>
public class AppExceptionHandler(
    IEnumerable<IExceptionMapper> mappers,
    ILogger<AppExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var mapped = mappers.Select(m => m.Map(exception)).FirstOrDefault(pd => pd is not null);
        var status = mapped?.Status ?? StatusCodes.Status500InternalServerError;
        var title  = mapped?.Title  ?? "Unexpected error";
        var detail = mapped?.Detail ?? "An unexpected error occurred.";

        logger.LogError(exception, "Unhandled {ExceptionType} on {Method} {Path}",
            exception.GetType().Name, context.Request.Method, context.Request.Path);

        context.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext    = context,
            ProblemDetails = { Title = title, Detail = detail, Status = status }
        });
    }
}
