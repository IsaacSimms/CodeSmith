// == App Exception Handler == //
using CodeSmith.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CodeSmith.Api.Middleware;

/// <summary>
/// Maps domain exceptions to HTTP status codes and RFC 7807 ProblemDetails responses
/// via the ASP.NET 8 IExceptionHandler pipeline.
/// </summary>
public class AppExceptionHandler(
    ILogger<AppExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title, detail) = MapException(exception);

        logger.LogError(exception, "Unhandled {ExceptionType} on {Method} {Path}",
            exception.GetType().Name, context.Request.Method, context.Request.Path);

        context.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext    = context,
            ProblemDetails = { Title = title, Detail = detail, Status = status }
        });
    }

    public static (int Status, string Title, string Detail) MapException(Exception exception) => exception switch
    {
        SessionNotFoundException ex   => (StatusCodes.Status404NotFound,            "Session not found",    ex.Message),
        ChallengeNotFoundException ex => (StatusCodes.Status404NotFound,            "Challenge not found",  ex.Message),
        AiServiceException ex         => (StatusCodes.Status502BadGateway,          "AI service error",     ex.Message),
        CodeExecutionException ex     => (StatusCodes.Status500InternalServerError, "Code execution error", ex.Message),
        OperationCanceledException    => (StatusCodes.Status499ClientClosedRequest, "Request cancelled",    "Request was cancelled."),
        _                             => (StatusCodes.Status500InternalServerError, "Unexpected error",     "An unexpected error occurred.")
    };
}
