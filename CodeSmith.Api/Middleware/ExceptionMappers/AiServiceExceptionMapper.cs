// == AI Service Exception Mapper == //
using CodeSmith.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CodeSmith.Api.Middleware.ExceptionMappers;

public class AiServiceExceptionMapper : IExceptionMapper
{
    public ProblemDetails? Map(Exception exception)
    {
        if (exception is not AiServiceException ex) return null;
        return new ProblemDetails { Status = StatusCodes.Status502BadGateway, Title = "AI service error", Detail = ex.Message };
    }
}
