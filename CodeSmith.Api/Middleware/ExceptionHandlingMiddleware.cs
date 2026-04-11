// == Exception Handling Middleware == //
using System.Text.Json;
using CodeSmith.Core.Exceptions;

namespace CodeSmith.Api.Middleware;

/// <summary>
/// Global exception handling middleware that maps domain exceptions
/// to appropriate HTTP status codes without leaking stack traces.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = exception switch
        {
            SessionNotFoundException ex  => (StatusCodes.Status404NotFound, ex.Message),
            AnthropicApiException ex    => (StatusCodes.Status502BadGateway, ex.Message),
            CodeExecutionException ex   => (StatusCodes.Status500InternalServerError, ex.Message),
            OperationCanceledException => (StatusCodes.Status499ClientClosedRequest, "Request was cancelled."),
            _                          => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };

        // Log the real exception (not exposed to client)
        _logger.LogError(exception, "Unhandled exception processing {Method} {Path}",
            context.Request.Method, context.Request.Path);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = JsonSerializer.Serialize(new
        {
            error = message,
            statusCode
        });

        await context.Response.WriteAsync(response);
    }
}

// Extension method to register the exception handling middleware
public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
