// == Session Controller == //
using CodeSmith.Api.DTOs;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace CodeSmith.Api.Controllers;

/// <summary>
/// Handles coding problem session creation and chat interactions.
/// </summary>
[ApiController]
[Route("api/session")]
public class SessionController : ControllerBase
{
    private readonly IAnthropicService _anthropicService;

    public SessionController(IAnthropicService anthropicService)
    {
        _anthropicService = anthropicService;
    }

    // == Create Session Endpoint == //

    [HttpPost]  // Creates a new coding problem session at the specified difficulty level
    [ProducesResponseType(typeof(ProblemSession), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSession(
        [FromBody] CreateSessionRequest request,
        CancellationToken ct)
    {
        if (!Enum.IsDefined(typeof(Difficulty), request.Difficulty))
        {
            return BadRequest(new { error = "Invalid difficulty value. Use Easy, Medium, or Hard." });
        }

        var session = await _anthropicService.GenerateProblemAsync(request.Difficulty, ct);

        return CreatedAtAction(nameof(CreateSession), new { sessionId = session.SessionId }, session);
    }

    // == Chat Endpoint == //

    [HttpPost("{sessionId:guid}/chat")]  // Sends a message within an existing session and receives guided assistance
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Chat(
        Guid sessionId,
        [FromBody] ChatRequest request,
        CancellationToken ct)
    {
        var response = await _anthropicService.GetGuidanceAsync(sessionId, request.Message, ct);

        return Ok(new ChatResponse { Response = response });
    }
}
