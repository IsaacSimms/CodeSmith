// == App Exception Handler Tests == //
using CodeSmith.Api.Middleware;
using CodeSmith.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CodeSmith.Tests.Api;

/// <summary>
/// Pins the exception → status mapping table end-to-end through the handler: every mapped domain
/// exception (including the money-facing 402/400s), subtype matching (TaskCanceledException must
/// hit the OperationCanceledException row → 499), and the no-leak fallback for unmapped exceptions.
/// </summary>
public class AppExceptionHandlerTests
{
    private readonly ILogger<AppExceptionHandler> _logger         = Substitute.For<ILogger<AppExceptionHandler>>();
    private readonly IProblemDetailsService       _problemDetails = Substitute.For<IProblemDetailsService>();

    private AppExceptionHandler CreateHandler() => new(_logger, _problemDetails);

    private static DefaultHttpContext MakeContext()
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    // == Status Code Mapping — the full table, one row per mapped exception == //

    public static TheoryData<Exception, int> ExceptionStatusCases => new()
    {
        { new SessionNotFoundException(Guid.NewGuid()),          404 },
        { new ChallengeNotFoundException("ch-1"),                404 },
        { new ScenarioNotFoundException("sc-1"),                 404 },
        { new AiServiceException("upstream error"),              502 },
        { new CodeExecutionException("exec error"),              500 },
        { new OperationCanceledException(),                      499 },
        { new TaskCanceledException(),                           499 },   // subtype must match the OCE row (HttpClient cancellations)
        { new InsufficientQuotaException("user-1", "no quota"),  402 },   // the paywall signal
        { new InvalidPriceException("price_bad"),                400 },
        { new WebhookSignatureException("bad signature"),        400 },
        { new InvalidOperationException("unexpected"),           500 },   // unmapped → fallback
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

    // == Detail semantics: mapped → ex.Message, cancelled → fixed text, unmapped → no leak == //

    [Fact]
    public async Task TryHandleAsync_MappedException_UsesExceptionMessageAsDetail()
    {
        ProblemDetailsContext? captured = null;
        _problemDetails.TryWriteAsync(Arg.Do<ProblemDetailsContext>(c => captured = c)).Returns(true);

        await CreateHandler().TryHandleAsync(MakeContext(), new InsufficientQuotaException("user-1", "Out of free quota and paid credits."), default);

        Assert.NotNull(captured);
        Assert.Equal("Insufficient quota or credits", captured.ProblemDetails.Title);
        Assert.Equal("Out of free quota and paid credits.", captured.ProblemDetails.Detail);
    }

    [Fact]
    public async Task TryHandleAsync_Cancellation_UsesFixedDetail_NotExceptionMessage()
    {
        ProblemDetailsContext? captured = null;
        _problemDetails.TryWriteAsync(Arg.Do<ProblemDetailsContext>(c => captured = c)).Returns(true);

        await CreateHandler().TryHandleAsync(MakeContext(), new TaskCanceledException("internal transport details"), default);

        Assert.NotNull(captured);
        Assert.Equal("Request cancelled", captured.ProblemDetails.Title);
        Assert.Equal("Request was cancelled.", captured.ProblemDetails.Detail);
        Assert.DoesNotContain("internal transport details", captured.ProblemDetails.Detail);
    }

    [Fact]
    public async Task TryHandleAsync_UnknownException_DoesNotLeakMessage()
    {
        ProblemDetailsContext? captured = null;
        _problemDetails.TryWriteAsync(Arg.Do<ProblemDetailsContext>(c => captured = c)).Returns(true);

        await CreateHandler().TryHandleAsync(MakeContext(), new InvalidOperationException("sensitive internal details"), default);

        Assert.NotNull(captured);
        Assert.Equal("An unexpected error occurred.", captured.ProblemDetails.Detail);
        Assert.DoesNotContain("sensitive internal details", captured.ProblemDetails.Detail);
    }
}
