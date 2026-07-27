// == Executor Code Execution Service Tests == //
using System.Net;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Configuration;
using CodeSmith.Infrastructure.Services.Executor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CodeSmith.Tests.Infrastructure;

public class ExecutorCodeExecutionServiceTests
{
    private const string BaseUrl = "https://ca-codesmith-exec-001.internal.example.azurecontainerapps.io";

    private static ExecutorCodeExecutionService CreateService(
        CapturingHttpHandler handler,
        int maxOutputLength = 10_000)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl + "/") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(ExecutorHttpClient.Name).Returns(httpClient);

        var options = Options.Create(new CodeExecutionOptions
        {
            Backend = "Executor",
            Executor = new ExecutorOptions
            {
                BaseUrl = BaseUrl,
                ExecutePath = "/execute",
                TimeoutSeconds = 120,
                RunTimeoutMs = 10_000,
                CompileTimeoutMs = 10_000,
                MaxOutputLength = maxOutputLength
            }
        });
        var logger = Substitute.For<ILogger<ExecutorCodeExecutionService>>();
        return new ExecutorCodeExecutionService(factory, options, logger);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulRun_ReturnsStdoutAndExitCode()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.OK, """
            { "stdout":"hello\n","stderr":"","exitCode":0,"timedOut":false }
            """);
        var service = CreateService(handler);

        var result = await service.ExecuteAsync(new CodeExecutionRequest
        {
            Language = Language.Python,
            Code = "print('hello')"
        });

        Assert.Equal("hello\n", result.Stdout);
        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.Equal("/execute", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("\"language\":\"python\"", handler.LastRequestBody);
    }

    // Distinguishes this Adapter from the Dynamic Sessions one: internal ingress is the trust
    // boundary, so no bearer token is attached and no pool identifier is appended.
    [Fact]
    public async Task ExecuteAsync_SendsNoAuthHeaderAndNoIdentifier()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.OK, """
            { "stdout":"","stderr":"","exitCode":0,"timedOut":false }
            """);
        var service = CreateService(handler);

        await service.ExecuteAsync(new CodeExecutionRequest
        {
            Language = Language.Python,
            Code = "print(1)",
            SessionId = Guid.NewGuid()
        });

        Assert.Null(handler.LastRequest!.Headers.Authorization);
        Assert.DoesNotContain("identifier", handler.LastRequest.RequestUri!.Query);
    }

    // Piston and LocalProcess already treat SessionId as optional; this Adapter must too,
    // since it has no pool to key a sandbox against.
    [Fact]
    public async Task ExecuteAsync_WithoutSessionId_Succeeds()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.OK, """
            { "stdout":"ok","stderr":"","exitCode":0,"timedOut":false }
            """);
        var service = CreateService(handler);

        var result = await service.ExecuteAsync(new CodeExecutionRequest
        {
            Language = Language.Go,
            Code = "package main"
        });

        Assert.Equal("ok", result.Stdout);
    }

    [Fact]
    public async Task ExecuteAsync_TimedOut_SetsFlags()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.OK, """
            { "stdout":"","stderr":"killed","exitCode":1,"timedOut":true }
            """);
        var service = CreateService(handler);

        var result = await service.ExecuteAsync(new CodeExecutionRequest
        {
            Language = Language.Python,
            Code = "while True: pass"
        });

        Assert.True(result.TimedOut);
        Assert.Equal(-1, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_HttpFailure_SurfacesBody()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.BadRequest, "bad request body");
        var service = CreateService(handler);

        var ex = await Assert.ThrowsAsync<CodeExecutionException>(() =>
            service.ExecuteAsync(new CodeExecutionRequest
            {
                Language = Language.Python,
                Code = "print(1)"
            }));

        Assert.Contains("400", ex.Message);
        Assert.Contains("bad request body", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ConnectionFailure_ThrowsCodeExecutionException()
    {
        var handler = new CapturingHttpHandler(new HttpRequestException("boom"));
        var service = CreateService(handler);

        var ex = await Assert.ThrowsAsync<CodeExecutionException>(() =>
            service.ExecuteAsync(new CodeExecutionRequest
            {
                Language = Language.Python,
                Code = "print(1)"
            }));

        Assert.Contains("sandbox unavailable", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_OutputExceedsMaxLength_IsTruncated()
    {
        var longOutput = new string('x', 50);
        var handler = new CapturingHttpHandler(HttpStatusCode.OK, $$"""
            { "stdout":"{{longOutput}}","stderr":"","exitCode":0,"timedOut":false }
            """);
        var service = CreateService(handler, maxOutputLength: 10);

        var result = await service.ExecuteAsync(new CodeExecutionRequest
        {
            Language = Language.Python,
            Code = "print('x'*50)"
        });

        Assert.Contains("[output truncated]", result.Stdout);
    }
}
