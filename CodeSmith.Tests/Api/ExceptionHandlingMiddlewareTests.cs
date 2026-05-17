// == App Exception Handler Tests == //
using CodeSmith.Api.Middleware;
using CodeSmith.Api.Middleware.ExceptionMappers;
using CodeSmith.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CodeSmith.Tests.Api;

public class AppExceptionHandlerTests
{
    private readonly ILogger<AppExceptionHandler> _logger         = Substitute.For<ILogger<AppExceptionHandler>>();
    private readonly IProblemDetailsService        _problemDetails = Substitute.For<IProblemDetailsService>();

    private AppExceptionHandler CreateHandler() => new(
        [
            new SessionNotFoundExceptionMapper(),
            new ChallengeNotFoundExceptionMapper(),
            new AiServiceExceptionMapper(),
            new CodeExecutionExceptionMapper(),
            new OperationCancelledExceptionMapper()
        ],
        _logger,
        _problemDetails);

    private static DefaultHttpContext MakeContext()
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    // == Status Code Mapping (integration: real mappers wired through handler) == //

    public static TheoryData<Exception, int> ExceptionStatusCases => new()
    {
        { new SessionNotFoundException(Guid.NewGuid()), 404 },
        { new ChallengeNotFoundException("ch-1"),       404 },
        { new AiServiceException("upstream error"),     502 },
        { new CodeExecutionException("exec error"),     500 },
        { new InvalidOperationException("unexpected"),  500 },
    };

    [Theory]
    [MemberData(nameof(ExceptionStatusCases))]
    public async Task TryHandleAsync_SetsCorrectStatusCode(Exception exception, int expectedStatus)
    {
        _problemDetails.TryWriteAsync(Arg.Any<ProblemDetailsContext>()).Returns(true);

        var context = MakeContext();
        await CreateHandler().TryHandleAsync(context, exception, default);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsTrueWhenProblemDetailsHandled()
    {
        _problemDetails.TryWriteAsync(Arg.Any<ProblemDetailsContext>()).Returns(true);

        var result = await CreateHandler().TryHandleAsync(MakeContext(), new AiServiceException("error"), default);

        Assert.True(result);
    }

    [Fact]
    public async Task TryHandleAsync_UnknownException_DoesNotLeakMessage()
    {
        _problemDetails.TryWriteAsync(Arg.Any<ProblemDetailsContext>()).Returns(true);

        ProblemDetailsContext? captured = null;
        _problemDetails
            .When(x => x.TryWriteAsync(Arg.Any<ProblemDetailsContext>()))
            .Do(call => captured = call.Arg<ProblemDetailsContext>());

        await CreateHandler().TryHandleAsync(MakeContext(), new InvalidOperationException("sensitive internal details"), default);

        Assert.NotNull(captured);
        Assert.Equal("An unexpected error occurred.", captured.ProblemDetails.Detail);
        Assert.DoesNotContain("sensitive internal details", captured.ProblemDetails.Detail);
    }
}

// == Per-Mapper Unit Tests == //

public class ExceptionMapperTests
{
    // == SessionNotFoundExceptionMapper == //

    [Fact]
    public void SessionNotFound_MapsCorrectly()
    {
        var id     = Guid.NewGuid();
        var result = new SessionNotFoundExceptionMapper().Map(new SessionNotFoundException(id));

        Assert.NotNull(result);
        Assert.Equal(404, result.Status);
        Assert.Equal("Session not found", result.Title);
        Assert.Contains(id.ToString(), result.Detail);
    }

    [Fact]
    public void SessionNotFound_ReturnsNullForOtherExceptions()
        => Assert.Null(new SessionNotFoundExceptionMapper().Map(new InvalidOperationException()));

    // == ChallengeNotFoundExceptionMapper == //

    [Fact]
    public void ChallengeNotFound_MapsCorrectly()
    {
        var result = new ChallengeNotFoundExceptionMapper().Map(new ChallengeNotFoundException("ch-99"));

        Assert.NotNull(result);
        Assert.Equal(404, result.Status);
        Assert.Equal("Challenge not found", result.Title);
    }

    [Fact]
    public void ChallengeNotFound_ReturnsNullForOtherExceptions()
        => Assert.Null(new ChallengeNotFoundExceptionMapper().Map(new InvalidOperationException()));

    // == AiServiceExceptionMapper == //

    [Fact]
    public void AiService_MapsCorrectly()
    {
        var result = new AiServiceExceptionMapper().Map(new AiServiceException("upstream down"));

        Assert.NotNull(result);
        Assert.Equal(502, result.Status);
        Assert.Equal("AI service error", result.Title);
        Assert.Contains("upstream down", result.Detail);
    }

    [Fact]
    public void AiService_ReturnsNullForOtherExceptions()
        => Assert.Null(new AiServiceExceptionMapper().Map(new InvalidOperationException()));

    // == CodeExecutionExceptionMapper == //

    [Fact]
    public void CodeExecution_MapsCorrectly()
    {
        var result = new CodeExecutionExceptionMapper().Map(new CodeExecutionException("timeout"));

        Assert.NotNull(result);
        Assert.Equal(500, result.Status);
        Assert.Equal("Code execution error", result.Title);
        Assert.Contains("timeout", result.Detail);
    }

    [Fact]
    public void CodeExecution_ReturnsNullForOtherExceptions()
        => Assert.Null(new CodeExecutionExceptionMapper().Map(new InvalidOperationException()));

    // == OperationCancelledExceptionMapper == //

    [Fact]
    public void OperationCancelled_MapsCorrectly()
    {
        var result = new OperationCancelledExceptionMapper().Map(new OperationCanceledException());

        Assert.NotNull(result);
        Assert.Equal(499, result.Status);
        Assert.Equal("Request cancelled", result.Title);
        Assert.Equal("Request was cancelled.", result.Detail);
    }

    [Fact]
    public void OperationCancelled_ReturnsNullForOtherExceptions()
        => Assert.Null(new OperationCancelledExceptionMapper().Map(new InvalidOperationException()));
}
