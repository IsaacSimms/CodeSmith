// == Insufficient Quota Exception Mapper == //
using CodeSmith.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CodeSmith.Api.Middleware.ExceptionMappers;

public class InsufficientQuotaExceptionMapper : IExceptionMapper
{
    public ProblemDetails? Map(Exception exception)
    {
        if (exception is not InsufficientQuotaException ex) return null;

        return new ProblemDetails
        {
            Status = StatusCodes.Status402PaymentRequired,
            Title = "Insufficient quota or credits",
            Detail = ex.Message,
            Type = "https://httpstatuses.com/402"
        };
    }
}
