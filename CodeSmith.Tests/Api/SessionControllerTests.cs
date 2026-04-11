// == Session Controller Tests == //
using CodeSmith.Api.Controllers;
using CodeSmith.Api.DTOs;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace CodeSmith.Tests.Api;

public class SessionControllerTests
{
    private readonly IAnthropicService _anthropicService = Substitute.For<IAnthropicService>();
    private readonly SessionController _controller;

    public SessionControllerTests()
    {
        _controller = new SessionController(_anthropicService);
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

        _anthropicService
            .GenerateProblemAsync(Difficulty.Easy, Language.CSharp, Arg.Any<CancellationToken>())
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
        _anthropicService
            .GenerateProblemAsync(Difficulty.Medium, language, Arg.Any<CancellationToken>())
            .Returns(new ProblemSession { Difficulty = Difficulty.Medium, Language = language });

        var result = await _controller.CreateSession(
            new CreateSessionRequest { Difficulty = Difficulty.Medium, Language = language },
            CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result);
        await _anthropicService.Received(1).GenerateProblemAsync(Difficulty.Medium, language, Arg.Any<CancellationToken>());
    }

    // == Chat Tests == //

    [Fact]
    public async Task Chat_WithValidRequest_Returns200()
    {
        var sessionId = Guid.NewGuid();
        _anthropicService
            .GetGuidanceAsync(sessionId, "help", "int x = 1;", Arg.Any<CancellationToken>())
            .Returns("Here's a hint...");

        var result = await _controller.Chat(
            sessionId,
            new ChatRequest { Message = "help", EditorContent = "int x = 1;" },
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ChatResponse>(okResult.Value);
        Assert.Equal("Here's a hint...", response.Response);
    }

    [Fact]
    public async Task Chat_WithNullEditorContent_PassesNullToService()
    {
        var sessionId = Guid.NewGuid();
        _anthropicService
            .GetGuidanceAsync(sessionId, "help", null, Arg.Any<CancellationToken>())
            .Returns("Here's a hint...");

        var result = await _controller.Chat(
            sessionId,
            new ChatRequest { Message = "help" },
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<ChatResponse>(okResult.Value);
        await _anthropicService.Received(1).GetGuidanceAsync(sessionId, "help", null, Arg.Any<CancellationToken>());
    }
}
