// == Code Execution Exception Mapper == //
using CodeSmith.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CodeSmith.Api.Middleware.ExceptionMappers;

public class CodeExecutionExceptionMapper : IExceptionMapper
{
    public ProblemDetails? Map(Exception exception)
    {
        if (exception is not CodeExecutionException ex) return null;
        return new ProblemDetails { Status = StatusCodes.Status500InternalServerError, Title = "Code execution error", Detail = ex.Message };
    }
}
