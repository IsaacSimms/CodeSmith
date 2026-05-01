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
    private readonly ICodeExecutionService _codeExecutionService = Substitute.For<ICodeExecutionService>();
    private readonly ISessionStore _sessionStore = Substitute.For<ISessionStore>();
    private readonly SessionController _controller;

    public SessionControllerTests()
    {
        var aiOptions = Options.Create(new AiOptions { ActiveProvider = "Anthropic" });
        _controller = new SessionController(_tutoringService, _codeExecutionService, _sessionStore, aiOptions);
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
            new CreateSessionRequest { Difficulty = Difficulty.Easy, Language = Language.CSharp },
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
            new CreateSessionRequest { Difficulty = Difficulty.Medium, Language = language },
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
            .GetGuidanceAsync(sessionId, "help", "int x = 1;", Arg.Any<bool>(), Arg.Any<CancellationToken>())
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
            .GetGuidanceAsync(sessionId, "help", null, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new ChatResponse { Response = "Here's a hint..." });

        var result = await _controller.Chat(
            sessionId,
            new ChatRequest { Message = "help" },
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<ChatResponse>(okResult.Value);
        await _tutoringService.Received(1).GetGuidanceAsync(sessionId, "help", null, Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    // == RunCode Tests == //

    [Fact]
    public async Task RunCode_WithValidRequest_Returns200()
    {
        var sessionId = Guid.NewGuid();
        _sessionStore.Get(sessionId).Returns(new ProblemSession { SessionId = sessionId });
        _codeExecutionService
            .ExecuteAsync(Language.Python, "print('hi')", Arg.Any<CancellationToken>())
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
        _sessionStore.Get(sessionId).Returns((ProblemSession?)null);

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
        _sessionStore.Get(sessionId).Returns(new ProblemSession { SessionId = sessionId });
        _codeExecutionService
            .ExecuteAsync(Language.Python, "while True: pass", Arg.Any<CancellationToken>())
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
}
