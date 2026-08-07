// == App Exception Handler == //
using CodeSmith.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CodeSmith.Api.Middleware;

/// <summary>
/// Maps domain exceptions to RFC 7807 ProblemDetails responses via a declarative table.
/// Adding a new exception type is one table row; the lookup logic never changes. Entries are
/// matched in order with subtype semantics (e.g. TaskCanceledException hits the
/// OperationCanceledException row), and an unmapped exception falls back to a 500 whose detail
/// never leaks the internal message.
/// </summary>
public class AppExceptionHandler(
    ILogger<AppExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    // == Exception → ProblemDetails mapping table == //
    // First entry whose type matches (subtypes included) wins. FixedDetail null → the exception's
    // own Message is safe to show; non-null → always use the fixed text instead.
    private static readonly (Type Type, int Status, string Title, string? FixedDetail)[] Mappings =
    [
        (typeof(SessionNotFoundException),   StatusCodes.Status404NotFound,            "Session not found",             null),
        (typeof(ChallengeNotFoundException), StatusCodes.Status404NotFound,            "Challenge not found",           null),
        (typeof(ScenarioNotFoundException),  StatusCodes.Status404NotFound,            "Scenario not found",            null),
        (typeof(AiServiceException),         StatusCodes.Status502BadGateway,          "AI service error",              null),
        (typeof(BillingServiceException),    StatusCodes.Status502BadGateway,          "Billing service error",         null),
        (typeof(CodeExecutionException),     StatusCodes.Status500InternalServerError, "Code execution error",          null),
        (typeof(OperationCanceledException), StatusCodes.Status499ClientClosedRequest, "Request cancelled",             "Request was cancelled."),
        (typeof(InsufficientQuotaException), StatusCodes.Status402PaymentRequired,     "Insufficient quota or credits", null),
        (typeof(InvalidPriceException),      StatusCodes.Status400BadRequest,          "Invalid price",                 null),
        (typeof(WebhookSignatureException),  StatusCodes.Status400BadRequest,          "Invalid webhook signature",     null),
    ];

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title, detail) = Map(exception);

        logger.LogError(exception, "Unhandled {ExceptionType} on {Method} {Path}",
            exception.GetType().Name, context.Request.Method, context.Request.Path);

        context.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext    = context,
            ProblemDetails = { Title = title, Detail = detail, Status = status }
        });
    }

    // == Table lookup == //

    // Internal so the NDJSON stream writer maps mid-stream failures to error events with the same
    // table — a failure after headers are sent cannot change the status line, but its code should
    // agree with what the status would have been.
    internal static (int Status, string Title, string Detail) Map(Exception exception)
    {
        foreach (var (type, status, title, fixedDetail) in Mappings)
        {
            if (type.IsInstanceOfType(exception))
                return (status, title, fixedDetail ?? exception.Message);
        }
        return (StatusCodes.Status500InternalServerError, "Unexpected error", "An unexpected error occurred.");
    }
}
