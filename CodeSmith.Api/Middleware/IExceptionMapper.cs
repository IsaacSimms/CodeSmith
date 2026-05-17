// == Exception Mapper Interface == //
using Microsoft.AspNetCore.Mvc;

namespace CodeSmith.Api.Middleware;

/// <summary>
/// Maps a domain exception to an RFC 7807 ProblemDetails response.
/// Returns null when the exception type is not handled by this mapper.
/// </summary>
public interface IExceptionMapper
{
    ProblemDetails? Map(Exception exception);
}
