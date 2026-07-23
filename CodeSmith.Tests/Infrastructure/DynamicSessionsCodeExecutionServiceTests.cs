// == Dynamic Sessions Code Execution Service Tests == //
using System.Net;
using System.Text;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Configuration;
using CodeSmith.Infrastructure.Services.DynamicSessions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CodeSmith.Tests.Infrastructure;

public class DynamicSessionsCodeExecutionServiceTests
{
    private static DynamicSessionsCodeExecutionService CreateService(
        StubHandler handler,
        out CapturingHandler capture,
        int maxOutputLength = 10_000)
    {
        capture = new CapturingHandler(handler);
        var httpClient = new HttpClient(capture) { BaseAddress = new Uri("https://pool.example.azurecontainerapps.io/") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(DynamicSessionsHttpClient.Name).Returns(httpClient);

        var tokens = Substitute.For<IDynamicSessionsTokenProvider>();
        tokens.GetAccessTokenAsync(Arg.Any<CancellationToken>()).Returns("test-token");

        var options = Options.Create(new CodeExecutionOptions
        {
            Backend = "DynamicSessions",
            DynamicSessions = new DynamicSessionsOptions
            {
                PoolManagementEndpoint = "https://pool.example.azurecontainerapps.io",
                ExecutePath = "/execute",
                TimeoutSeconds = 120,
                RunTimeoutMs = 10_000,
                CompileTimeoutMs = 10_000,
                MaxOutputLength = maxOutputLength
            }
        });
        var logger = Substitute.For<ILogger<DynamicSessionsCodeExecutionService>>();
        return new DynamicSessionsCodeExecutionService(factory, tokens, options, logger);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulRun_ReturnsStdoutAndExitCode()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """
            { "stdout":"hello\n","stderr":"","exitCode":0,"timedOut":false }
            """);
        var service = CreateService(handler, out var capture);
        var sessionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        var result = await service.ExecuteAsync(new CodeExecutionRequest
        {
            Language = Language.Python,
            Code = "print('hello')",
            SessionId = sessionId
        });

        Assert.Equal("hello\n", result.Stdout);
        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.NotNull(capture.LastRequest);
        Assert.Equal("Bearer", capture.LastRequest!.Headers.Authorization?.Scheme);
        Assert.Equal("test-token", capture.LastRequest.Headers.Authorization?.Parameter);
        Assert.Contains("identifier=aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", capture.LastRequest.RequestUri!.Query);
        Assert.EndsWith("/execute", capture.LastRequest.RequestUri.AbsolutePath);
    }

    [Fact]
    public async Task ExecuteAsync_MissingSessionId_Throws()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """
            { "stdout":"","stderr":"","exitCode":0,"timedOut":false }
            """);
        var service = CreateService(handler, out _);

        var ex = await Assert.ThrowsAsync<CodeExecutionException>(() =>
            service.ExecuteAsync(new CodeExecutionRequest
            {
                Language = Language.Python,
                Code = "print(1)"
            }));

        Assert.Contains("SessionId", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_TimedOut_SetsFlags()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """
            { "stdout":"","stderr":"killed","exitCode":1,"timedOut":true }
            """);
        var service = CreateService(handler, out _);

        var result = await service.ExecuteAsync(new CodeExecutionRequest
        {
            Language = Language.Python,
            Code = "while True: pass",
            SessionId = Guid.NewGuid()
        });

        Assert.True(result.TimedOut);
        Assert.Equal(-1, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_HttpFailure_SurfacesBody()
    {
        var handler = new StubHandler(HttpStatusCode.BadRequest, "bad request body");
        var service = CreateService(handler, out _);

        var ex = await Assert.ThrowsAsync<CodeExecutionException>(() =>
            service.ExecuteAsync(new CodeExecutionRequest
            {
                Language = Language.Python,
                Code = "print(1)",
                SessionId = Guid.NewGuid()
            }));

        Assert.Contains("400", ex.Message);
        Assert.Contains("bad request body", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ConnectionFailure_ThrowsCodeExecutionException()
    {
        var handler = new StubHandler(new HttpRequestException("boom"));
        var service = CreateService(handler, out _);

        var ex = await Assert.ThrowsAsync<CodeExecutionException>(() =>
            service.ExecuteAsync(new CodeExecutionRequest
            {
                Language = Language.Python,
                Code = "print(1)",
                SessionId = Guid.NewGuid()
            }));

        Assert.Contains("sandbox unavailable", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_OutputExceedsMaxLength_IsTruncated()
    {
        var longOutput = new string('x', 50);
        var handler = new StubHandler(HttpStatusCode.OK, $$"""
            { "stdout":"{{longOutput}}","stderr":"","exitCode":0,"timedOut":false }
            """);
        var service = CreateService(handler, out _, maxOutputLength: 10);

        var result = await service.ExecuteAsync(new CodeExecutionRequest
        {
            Language = Language.Python,
            Code = "print('x'*50)",
            SessionId = Guid.NewGuid()
        });

        Assert.Contains("[output truncated]", result.Stdout);
    }

    // == Test Helpers == //
    private sealed class CapturingHandler : DelegatingHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public CapturingHandler(HttpMessageHandler inner) : base(inner) { }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return base.SendAsync(request, cancellationToken);
        }
    }

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
