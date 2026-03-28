// == Exception Handling Middleware Tests == //
using System.Text.Json;
using CodeSmith.Api.Middleware;
using CodeSmith.Core.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CodeSmith.Tests.Api;

public class ExceptionHandlingMiddlewareTests
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger =
        Substitute.For<ILogger<ExceptionHandlingMiddleware>>();

    // == Helper == //
    private ExceptionHandlingMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new ExceptionHandlingMiddleware(next, _logger);
    }

    [Fact]
    public async Task SessionNotFoundException_Returns404()
    {
        var middleware = CreateMiddleware(_ =>
            throw new SessionNotFoundException(Guid.NewGuid()));

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(404, context.Response.StatusCode);
    }

    [Fact]
    public async Task AnthropicApiException_Returns502()
    {
        var middleware = CreateMiddleware(_ =>
            throw new AnthropicApiException("API error"));

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(502, context.Response.StatusCode);
    }

    [Fact]
    public async Task UnhandledException_Returns500()
    {
        var middleware = CreateMiddleware(_ =>
            throw new InvalidOperationException("something broke"));

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(500, context.Response.StatusCode);
    }

    [Fact]
    public async Task UnhandledException_DoesNotLeakStackTrace()
    {
        var middleware = CreateMiddleware(_ =>
            throw new InvalidOperationException("sensitive internal details"));

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(body);

        Assert.Equal("An unexpected error occurred.", json.GetProperty("error").GetString());
        Assert.DoesNotContain("sensitive internal details", body);
    }

    [Fact]
    public async Task NoException_PassesThrough()
    {
        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
    }
}
