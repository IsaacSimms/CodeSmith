// == Webhook Signature Exception Mapper == //
using CodeSmith.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CodeSmith.Api.Middleware.ExceptionMappers;

public class WebhookSignatureExceptionMapper : IExceptionMapper
{
    public ProblemDetails? Map(Exception exception)
    {
        if (exception is not WebhookSignatureException ex) return null;

        return new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid webhook signature",
            Detail = ex.Message,
            Type = "https://httpstatuses.com/400"
        };
    }
}
