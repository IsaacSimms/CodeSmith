// == Challenge Not Found Mapper == //
using CodeSmith.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CodeSmith.Api.Middleware.ExceptionMappers;

public class ChallengeNotFoundExceptionMapper : IExceptionMapper
{
    public ProblemDetails? Map(Exception exception)
    {
        if (exception is not ChallengeNotFoundException ex) return null;
        return new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Challenge not found", Detail = ex.Message };
    }
}
