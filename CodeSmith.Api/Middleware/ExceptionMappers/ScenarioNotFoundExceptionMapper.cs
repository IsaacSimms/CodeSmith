// == Scenario Not Found Mapper == //
using CodeSmith.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CodeSmith.Api.Middleware.ExceptionMappers;

public class ScenarioNotFoundExceptionMapper : IExceptionMapper
{
    public ProblemDetails? Map(Exception exception)
    {
        if (exception is not ScenarioNotFoundException ex) return null;
        return new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Scenario not found", Detail = ex.Message };
    }
}
