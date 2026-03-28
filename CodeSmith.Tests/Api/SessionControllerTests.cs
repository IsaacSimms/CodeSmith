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
    public async Task CreateSession_WithValidDifficulty_Returns201()
    {
        var expectedSession = new ProblemSession
        {
            Difficulty = Difficulty.Easy,
            ProblemDescription = "Test problem",
            StarterCode = "public class Solution { }"
        };

        _anthropicService
            .GenerateProblemAsync(Difficulty.Easy, Arg.Any<CancellationToken>())
            .Returns(expectedSession);

        var result = await _controller.CreateSession(
            new CreateSessionRequest { Difficulty = Difficulty.Easy },
            CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, createdResult.StatusCode);

        var session = Assert.IsType<ProblemSession>(createdResult.Value);
        Assert.Equal("Test problem", session.ProblemDescription);
    }

    [Fact]
    public async Task CreateSession_WithInvalidDifficulty_Returns400()
    {
        var result = await _controller.CreateSession(
            new CreateSessionRequest { Difficulty = (Difficulty)999 },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    // == Chat Tests == //

    [Fact]
    public async Task Chat_WithValidRequest_Returns200()
    {
        var sessionId = Guid.NewGuid();
        _anthropicService
            .GetGuidanceAsync(sessionId, "help", Arg.Any<CancellationToken>())
            .Returns("Here's a hint...");

        var result = await _controller.Chat(
            sessionId,
            new ChatRequest { Message = "help" },
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ChatResponse>(okResult.Value);
        Assert.Equal("Here's a hint...", response.Response);
    }
}
