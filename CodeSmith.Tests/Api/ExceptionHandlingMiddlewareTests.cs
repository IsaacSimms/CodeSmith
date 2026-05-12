// == App Exception Handler Tests == //
using CodeSmith.Api.Middleware;
using CodeSmith.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CodeSmith.Tests.Api;

public class AppExceptionHandlerTests
{
    private readonly ILogger<AppExceptionHandler> _logger           = Substitute.For<ILogger<AppExceptionHandler>>();
    private readonly IProblemDetailsService        _problemDetails   = Substitute.For<IProblemDetailsService>();

    private AppExceptionHandler CreateHandler() => new(_logger, _problemDetails);

    private static DefaultHttpContext MakeContext()
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    // == Status Code Mapping == //

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

    // == Safe Detail Mapping == //

    [Fact]
    public void MapException_UnknownException_DoesNotLeakMessage()
    {
        var (_, _, detail) = AppExceptionHandler.MapException(
            new InvalidOperationException("sensitive internal details"));

        Assert.Equal("An unexpected error occurred.", detail);
        Assert.DoesNotContain("sensitive internal details", detail);
    }

    [Fact]
    public void MapException_SessionNotFoundException_ExposesSessionId()
    {
        var id = Guid.NewGuid();
        var (status, _, detail) = AppExceptionHandler.MapException(new SessionNotFoundException(id));

        Assert.Equal(404, status);
        Assert.Contains(id.ToString(), detail);
    }

    [Fact]
    public void MapException_OperationCancelledException_UsesSafeMessage()
    {
        var (status, _, detail) = AppExceptionHandler.MapException(new OperationCanceledException());

        Assert.Equal(499, status);
        Assert.Equal("Request was cancelled.", detail);
    }
}
