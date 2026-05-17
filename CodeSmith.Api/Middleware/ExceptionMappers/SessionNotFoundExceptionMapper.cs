// == Session Not Found Mapper == //
using CodeSmith.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CodeSmith.Api.Middleware.ExceptionMappers;

public class SessionNotFoundExceptionMapper : IExceptionMapper
{
    public ProblemDetails? Map(Exception exception)
    {
        if (exception is not SessionNotFoundException ex) return null;
        return new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Session not found", Detail = ex.Message };
    }
}
