// == Session Controller Tests == //
using CodeSmith.Api.Controllers;
using CodeSmith.Api.DTOs;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CodeSmith.Tests.Api;

public class SessionControllerTests
{
    private readonly ITutoringService _tutoringService = Substitute.For<ITutoringService>();
    private readonly SessionController _controller;

    public SessionControllerTests()
    {
        var aiOptions = Options.Create(new AiOptions { ActiveProvider = "Xai" });
        _controller = new SessionController(_tutoringService, aiOptions);
    }

    // == Providers Endpoint == //

    [Fact]
    public void GetProviders_ReportsConfiguredActiveProvider()
    {
        var result = Assert.IsType<OkObjectResult>(_controller.GetProviders());

        // activeProvider comes from AiOptions (now "Xai" by default)
        var activeProvider = result.Value!.GetType().GetProperty("activeProvider")!.GetValue(result.Value);
        Assert.Equal("Xai", activeProvider);
    }

    // == CreateSession Tests == //

    [Fact]
    public async Task CreateSession_WithValidDifficultyAndLanguage_Returns201()
    {
        var expectedSession = new ProblemSession
        {
            Difficulty = Difficulty.Easy,
            Language = Language.CSharp,
            ProblemDescription = "Test problem",
            StarterCode = "// stub"
        };

        _tutoringService
            .GenerateProblemAsync(Difficulty.Easy, Language.CSharp, AiProvider.Anthropic, Arg.Any<CancellationToken>())
            .Returns(expectedSession);

        var result = await _controller.CreateSession(
            new CreateSessionRequest { Difficulty = Difficulty.Easy, Language = Language.CSharp, Provider = AiProvider.Anthropic },
            CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, createdResult.StatusCode);

        var session = Assert.IsType<ProblemSession>(createdResult.Value);
        Assert.Equal("Test problem", session.ProblemDescription);
        Assert.Equal(Language.CSharp, session.Language);
    }

    [Fact]
    public async Task CreateSession_WithInvalidDifficulty_Returns400()
    {
        var result = await _controller.CreateSession(
            new CreateSessionRequest { Difficulty = (Difficulty)999, Language = Language.CSharp },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task CreateSession_WithInvalidLanguage_Returns400()
    {
        var result = await _controller.CreateSession(
            new CreateSessionRequest { Difficulty = Difficulty.Easy, Language = (Language)999 },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task CreateSession_WithInvalidProvider_Returns400()
    {
        var result = await _controller.CreateSession(
            new CreateSessionRequest { Difficulty = Difficulty.Easy, Language = Language.CSharp, Provider = (AiProvider)999 },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Theory]
    [InlineData(Language.CSharp)]
    [InlineData(Language.Cpp)]
    [InlineData(Language.Go)]
    [InlineData(Language.Rust)]
    [InlineData(Language.Python)]
    [InlineData(Language.Java)]
    [InlineData(Language.TypeScript)]
    public async Task CreateSession_ForwardsLanguageToService(Language language)
    {
        _tutoringService
            .GenerateProblemAsync(Difficulty.Medium, language, AiProvider.Anthropic, Arg.Any<CancellationToken>())
            .Returns(new ProblemSession { Difficulty = Difficulty.Medium, Language = language });

        var result = await _controller.CreateSession(
            new CreateSessionRequest { Difficulty = Difficulty.Medium, Language = language, Provider = AiProvider.Anthropic },
            CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result);
        await _tutoringService.Received(1).GenerateProblemAsync(Difficulty.Medium, language, AiProvider.Anthropic, Arg.Any<CancellationToken>());
    }

    // == Chat Tests == //

    [Fact]
    public async Task Chat_WithValidRequest_Returns200()
    {
        var sessionId = Guid.NewGuid();
        _tutoringService
            .GetGuidanceAsync(sessionId, "help", "int x = 1;", Arg.Any<GuidanceMode>(), Arg.Any<CancellationToken>())
            .Returns(new ChatResponse { Response = "Here's a hint...", ContextTokensUsed = 1234, ContextWindowSize = 200_000 });

        var result = await _controller.Chat(
            sessionId,
            new ChatRequest { Message = "help", EditorContent = "int x = 1;" },
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ChatResponse>(okResult.Value);
        Assert.Equal("Here's a hint...", response.Response);
        Assert.Equal(1234, response.ContextTokensUsed);
        Assert.Equal(200_000, response.ContextWindowSize);
    }

    [Fact]
    public async Task Chat_WithNullEditorContent_PassesNullToService()
    {
        var sessionId = Guid.NewGuid();
        _tutoringService
            .GetGuidanceAsync(sessionId, "help", null, Arg.Any<GuidanceMode>(), Arg.Any<CancellationToken>())
            .Returns(new ChatResponse { Response = "Here's a hint..." });

        var result = await _controller.Chat(
            sessionId,
            new ChatRequest { Message = "help" },
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<ChatResponse>(okResult.Value);
        await _tutoringService.Received(1).GetGuidanceAsync(sessionId, "help", null, Arg.Any<GuidanceMode>(), Arg.Any<CancellationToken>());
    }

    // == RunCode Tests == //

    [Fact]
    public async Task RunCode_WithValidRequest_Returns200()
    {
        var sessionId = Guid.NewGuid();
        _tutoringService
            .RunCodeAsync(sessionId, Language.Python, "print('hi')", Arg.Any<CancellationToken>())
            .Returns(new CodeExecutionResult { Stdout = "hi\n", Stderr = "", ExitCode = 0, TimedOut = false });

        var result = await _controller.RunCode(
            sessionId,
            new RunCodeRequest { Code = "print('hi')", Language = Language.Python },
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RunCodeResponse>(okResult.Value);
        Assert.Equal("hi\n", response.Stdout);
        Assert.Equal(0, response.ExitCode);
        Assert.False(response.TimedOut);
    }

    [Fact]
    public async Task RunCode_WithInvalidSession_ThrowsSessionNotFound()
    {
        var sessionId = Guid.NewGuid();
        _tutoringService
            .RunCodeAsync(sessionId, Language.Python, "print('hi')", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<CodeExecutionResult>(new SessionNotFoundException(sessionId)));

        await Assert.ThrowsAsync<SessionNotFoundException>(() =>
            _controller.RunCode(
                sessionId,
                new RunCodeRequest { Code = "print('hi')", Language = Language.Python },
                CancellationToken.None));
    }

    [Fact]
    public async Task RunCode_WithTimedOutExecution_ReturnsTimedOutFlag()
    {
        var sessionId = Guid.NewGuid();
        _tutoringService
            .RunCodeAsync(sessionId, Language.Python, "while True: pass", Arg.Any<CancellationToken>())
            .Returns(new CodeExecutionResult { Stdout = "", Stderr = "Process killed: execution exceeded 10 second timeout.", ExitCode = -1, TimedOut = true });

        var result = await _controller.RunCode(
            sessionId,
            new RunCodeRequest { Code = "while True: pass", Language = Language.Python },
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RunCodeResponse>(okResult.Value);
        Assert.True(response.TimedOut);
        Assert.Equal(-1, response.ExitCode);
    }

    // == Streaming Endpoints (NDJSON chunk contract) == //

    private (SessionController Controller, MemoryStream Body) StreamingController()
    {
        var (context, body) = NdjsonEndpointHarness.CreateStreamingContext();
        var controller = new SessionController(_tutoringService, Options.Create(new AiOptions { ActiveProvider = "Xai" }))
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
        return (controller, body);
    }

    [Fact]
    public async Task ChatStream_WritesDeltasThenFinalWithChatMetadata()
    {
        var sessionId = Guid.NewGuid();
        _tutoringService
            .StreamGuidanceAsync(sessionId, "hi", null, GuidanceMode.Guidance, Arg.Any<Func<string, CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var onDelta = callInfo.Arg<Func<string, CancellationToken, Task>>();
                await onDelta("Hel", CancellationToken.None);
                await onDelta("lo!", CancellationToken.None);
                return new ChatResponse { Response = "Hello!", ContextTokensUsed = 42, ContextWindowSize = 200_000 };
            });
        var (controller, body) = StreamingController();

        await controller.ChatStream(sessionId, new ChatRequest { Message = "hi" }, CancellationToken.None);

        Assert.Equal("application/x-ndjson", controller.Response.ContentType);
        var events = NdjsonEndpointHarness.ReadEvents(body);
        Assert.Equal(3, events.Count);
        Assert.Equal("delta", events[0].GetProperty("type").GetString());
        Assert.Equal("Hel",   events[0].GetProperty("text").GetString());
        Assert.Equal("lo!",   events[1].GetProperty("text").GetString());
        Assert.Equal("final", events[2].GetProperty("type").GetString());
        Assert.Equal("Hello!", events[2].GetProperty("data").GetProperty("response").GetString());
        Assert.Equal(42, events[2].GetProperty("data").GetProperty("contextTokensUsed").GetInt32());
    }

    [Fact]
    public async Task ChatStream_MidStreamFailure_WritesErrorEventWithMappedStatusCode()
    {
        // The status line is frozen after the first delta, so the 502 the request would have had
        // must ride the stream as an error event instead.
        var sessionId = Guid.NewGuid();
        async Task<ChatResponse> DieAfterOneDelta(NSubstitute.Core.CallInfo callInfo)
        {
            var onDelta = callInfo.Arg<Func<string, CancellationToken, Task>>();
            await onDelta("part", CancellationToken.None);
            throw new AiServiceException("provider fell over");
        }
        _tutoringService
            .StreamGuidanceAsync(sessionId, "hi", null, GuidanceMode.Guidance, Arg.Any<Func<string, CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(DieAfterOneDelta);
        var (controller, body) = StreamingController();

        await controller.ChatStream(sessionId, new ChatRequest { Message = "hi" }, CancellationToken.None);

        var events = NdjsonEndpointHarness.ReadEvents(body);
        Assert.Equal("delta", events[0].GetProperty("type").GetString());
        Assert.Equal("error", events[^1].GetProperty("type").GetString());
        Assert.Equal(502, events[^1].GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task ChatStream_PreStreamFailure_PropagatesForRealStatusMapping()
    {
        // Nothing was written yet, so the exception must reach AppExceptionHandler and become a real 404
        var sessionId = Guid.NewGuid();
        _tutoringService
            .StreamGuidanceAsync(sessionId, "hi", null, GuidanceMode.Guidance, Arg.Any<Func<string, CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns<ChatResponse>(_ => throw new SessionNotFoundException(sessionId));
        var (controller, body) = StreamingController();

        await Assert.ThrowsAsync<SessionNotFoundException>(
            () => controller.ChatStream(sessionId, new ChatRequest { Message = "hi" }, CancellationToken.None));

        Assert.Empty(NdjsonEndpointHarness.ReadEvents(body));
    }

    [Fact]
    public async Task CreateSessionStream_WritesDeltasResetAndFinalSession()
    {
        _tutoringService
            .StreamGenerateProblemAsync(Difficulty.Easy, Language.Python, AiProvider.Xai,
                Arg.Any<Func<string, CancellationToken, Task>>(), Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var onDelta = callInfo.Arg<Func<string, CancellationToken, Task>>();
                var onReset = callInfo.Arg<Func<CancellationToken, Task>>();
                await onDelta("half a prob", CancellationToken.None);   // attempt 1, later abandoned
                await onReset(CancellationToken.None);
                await onDelta("Whole problem", CancellationToken.None); // attempt 2
                return new ProblemSession { Difficulty = Difficulty.Easy, Language = Language.Python, Provider = AiProvider.Xai, ProblemDescription = "Whole problem", StarterCode = "def y():" };
            });
        var (controller, body) = StreamingController();

        await controller.CreateSessionStream(
            new CreateSessionRequest { Difficulty = Difficulty.Easy, Language = Language.Python, Provider = AiProvider.Xai },
            CancellationToken.None);

        var events = NdjsonEndpointHarness.ReadEvents(body);
        Assert.Equal(["delta", "reset", "delta", "final"], events.Select(e => e.GetProperty("type").GetString()).ToArray());
        Assert.Equal("Whole problem", events[^1].GetProperty("data").GetProperty("problemDescription").GetString());
        Assert.Equal("def y():",      events[^1].GetProperty("data").GetProperty("starterCode").GetString());
    }

    [Fact]
    public async Task CreateSessionStream_WithInvalidDifficulty_Returns400BeforeAnyWrite()
    {
        var (controller, body) = StreamingController();

        var result = await controller.CreateSessionStream(
            new CreateSessionRequest { Difficulty = (Difficulty)999, Language = Language.Python, Provider = AiProvider.Xai },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(NdjsonEndpointHarness.ReadEvents(body));
    }
}
