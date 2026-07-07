// == Invalid Price Exception Mapper == //
using CodeSmith.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CodeSmith.Api.Middleware.ExceptionMappers;

public class InvalidPriceExceptionMapper : IExceptionMapper
{
    public ProblemDetails? Map(Exception exception)
    {
        if (exception is not InvalidPriceException ex) return null;

        return new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid price",
            Detail = ex.Message,
            Type = "https://httpstatuses.com/400"
        };
    }
}
