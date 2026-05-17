// == Operation Cancelled Mapper == //
using Microsoft.AspNetCore.Mvc;

namespace CodeSmith.Api.Middleware.ExceptionMappers;

public class OperationCancelledExceptionMapper : IExceptionMapper
{
    public ProblemDetails? Map(Exception exception)
    {
        if (exception is not OperationCanceledException) return null;
        return new ProblemDetails { Status = StatusCodes.Status499ClientClosedRequest, Title = "Request cancelled", Detail = "Request was cancelled." };
    }
}
