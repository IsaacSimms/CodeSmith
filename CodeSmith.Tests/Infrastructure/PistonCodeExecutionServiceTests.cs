// == Piston Code Execution Service Tests == //
using System.Net;
using System.Text;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Infrastructure.Configuration;
using CodeSmith.Infrastructure.Services.Piston;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CodeSmith.Tests.Infrastructure;

public class PistonCodeExecutionServiceTests
{
    private static PistonCodeExecutionService CreateService(StubHandler handler, int maxOutputLength = 10_000)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:2000") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(PistonHttpClient.Name).Returns(httpClient);

        var resolver = Substitute.For<IPistonRuntimeResolver>();
        resolver.ResolveVersionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("3.10.0"));

        var options = Options.Create(new CodeExecutionOptions
        {
            Backend = "Piston",
            Piston = new PistonOptions
            {
                BaseUrl = "http://localhost:2000",
                TimeoutSeconds = 15,
                RunTimeoutMs = 10_000,
                CompileTimeoutMs = 10_000,
                MaxOutputLength = maxOutputLength
            }
        });
        var logger = Substitute.For<ILogger<PistonCodeExecutionService>>();
        return new PistonCodeExecutionService(factory, resolver, options, logger);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulRun_ReturnsStdoutAndExitCode()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """
            { "language":"python","version":"3.10.0","run":{"stdout":"hello\n","stderr":"","output":"hello\n","code":0,"signal":null} }
            """);
        var service = CreateService(handler);

        var result = await service.ExecuteAsync(Language.Python, "print('hello')");

        Assert.Equal("hello\n", result.Stdout);
        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task ExecuteAsync_NonZeroExitCode_ForwardsExitCode()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """
            { "language":"python","version":"3.10.0","run":{"stdout":"","stderr":"Traceback...","output":"","code":1,"signal":null} }
            """);
        var service = CreateService(handler);

        var result = await service.ExecuteAsync(Language.Python, "raise Exception()");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Traceback", result.Stderr);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task ExecuteAsync_SigkillSignal_SetsTimedOutTrue()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """
            { "language":"python","version":"3.10.0","run":{"stdout":"","stderr":"","output":"","code":null,"signal":"SIGKILL"} }
            """);
        var service = CreateService(handler);

        var result = await service.ExecuteAsync(Language.Python, "while True: pass");

        Assert.True(result.TimedOut);
        Assert.Equal(-1, result.ExitCode);
        Assert.Contains("exceeded", result.Stderr);
    }

    [Fact]
    public async Task ExecuteAsync_CompileFailure_ReturnsCompileStageAndSkipsRun()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """
            {
              "language":"c++","version":"10.2.0",
              "compile":{"stdout":"","stderr":"main.cpp: error: expected ';'","output":"","code":1,"signal":null},
              "run":{"stdout":"","stderr":"","output":"","code":null,"signal":null}
            }
            """);
        var service = CreateService(handler);

        var result = await service.ExecuteAsync(Language.Cpp, "int main() { return 0 }");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("expected ';'", result.Stderr);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task ExecuteAsync_HttpFailure_SurfacesPistonResponseBody()
    {
        var handler = new StubHandler(HttpStatusCode.BadRequest, "Requested runtime is unknown");
        var service = CreateService(handler);

        var ex = await Assert.ThrowsAsync<CodeExecutionException>(
            () => service.ExecuteAsync(Language.Python, "print('hi')"));

        Assert.Contains("400", ex.Message);
        Assert.Contains("Requested runtime is unknown", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ConnectionRefused_ThrowsCodeExecutionException()
    {
        var handler = new StubHandler(new HttpRequestException("Connection refused"));
        var service = CreateService(handler);

        var ex = await Assert.ThrowsAsync<CodeExecutionException>(
            () => service.ExecuteAsync(Language.Python, "print('hi')"));

        Assert.Contains("Piston unavailable", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_OutputExceedsMaxLength_IsTruncated()
    {
        var longOutput = new string('x', 50);
        var handler = new StubHandler(HttpStatusCode.OK, $$"""
            { "language":"python","version":"3.10.0","run":{"stdout":"{{longOutput}}","stderr":"","output":"","code":0,"signal":null} }
            """);
        var service = CreateService(handler, maxOutputLength: 10);

        var result = await service.ExecuteAsync(Language.Python, "print('x' * 50)");

        Assert.Contains("[output truncated]", result.Stdout);
        Assert.True(result.Stdout.Length < 50 + "[output truncated]".Length + 5);
    }

    // == Test Helpers == //
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage? _response;
        private readonly Exception? _exception;

        public StubHandler(HttpStatusCode status, string body)
        {
            _response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }

        public StubHandler(Exception exception) => _exception = exception;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_exception is not null) throw _exception;
            return Task.FromResult(_response!);
        }
    }
}
