// == Code Execution Service Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CodeSmith.Tests.Infrastructure;

public class CodeExecutionServiceTests
{
    private readonly CodeExecutionService _service;

    public CodeExecutionServiceTests()
    {
        var logger = Substitute.For<ILogger<CodeExecutionService>>();
        _service = new CodeExecutionService(logger);
    }

    // == Python Tests (most likely runtime to be installed) == //

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExecuteAsync_PythonStdout_CapturesOutput()
    {
        var result = await _service.ExecuteAsync(Language.Python, "print('hello world')");

        Assert.Equal("hello world\n", result.Stdout.Replace("\r\n", "\n"));
        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExecuteAsync_PythonStderr_CapturesError()
    {
        var result = await _service.ExecuteAsync(Language.Python, "import sys; sys.stderr.write('oops')");

        Assert.Contains("oops", result.Stderr);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExecuteAsync_PythonSyntaxError_ReturnsNonZeroExit()
    {
        var result = await _service.ExecuteAsync(Language.Python, "def broken(");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("SyntaxError", result.Stderr);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExecuteAsync_PythonInfiniteLoop_TimesOut()
    {
        var result = await _service.ExecuteAsync(Language.Python, "while True: pass");

        Assert.True(result.TimedOut);
    }

    // == Language Strategy Coverage == //

    [Theory]
    [InlineData(Language.CSharp)]
    [InlineData(Language.Cpp)]
    [InlineData(Language.Go)]
    [InlineData(Language.Rust)]
    [InlineData(Language.Python)]
    [InlineData(Language.Java)]
    [InlineData(Language.TypeScript)]
    public async Task ExecuteAsync_AllLanguages_DoNotThrowArgumentOutOfRange(Language language)
    {
        // Verifies that every Language enum maps to a valid execution strategy.
        // The actual execution may fail if the runtime is not installed, but it
        // should throw CodeExecutionException, not ArgumentOutOfRangeException.
        try
        {
            await _service.ExecuteAsync(language, "// placeholder");
        }
        catch (CodeExecutionException)
        {
            // Expected if the runtime is not installed
        }
    }

    // == Cancellation == //

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExecuteAsync_WithCancelledToken_ThrowsOperationCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.ExecuteAsync(Language.Python, "print('hi')", cts.Token));
    }
}
