// == Session Controller == //
using CodeSmith.Api.DTOs;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace CodeSmith.Api.Controllers;

/// <summary>
/// Handles coding problem session creation, chat interactions, and code execution.
/// </summary>
[ApiController]
[Route("api/session")]
public class SessionController : ControllerBase
{
    private readonly IAnthropicService _anthropicService;
    private readonly ICodeExecutionService _codeExecutionService;
    private readonly ISessionStore _sessionStore;

    public SessionController(
        IAnthropicService anthropicService,
        ICodeExecutionService codeExecutionService,
        ISessionStore sessionStore)
    {
        _anthropicService = anthropicService;
        _codeExecutionService = codeExecutionService;
        _sessionStore = sessionStore;
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

        if (!Enum.IsDefined(typeof(Language), request.Language))
        {
            return BadRequest(new { error = "Invalid language value. Use CSharp, Cpp, Go, Rust, Python, Java, or TypeScript." });
        }

        var session = await _anthropicService.GenerateProblemAsync(request.Difficulty, request.Language, ct);

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
        var response = await _anthropicService.GetGuidanceAsync(sessionId, request.Message, request.EditorContent, request.IsCodeAnalysis, ct);

        return Ok(new ChatResponse { Response = response });
    }

    // == Run Code Endpoint == //

    [HttpPost("{sessionId:guid}/run")]  // Executes user code in a sandboxed process with a 10-second timeout
    [ProducesResponseType(typeof(RunCodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RunCode(
        Guid sessionId,
        [FromBody] RunCodeRequest request,
        CancellationToken ct)
    {
        var session = _sessionStore.Get(sessionId);
        if (session is null)
            throw new SessionNotFoundException(sessionId);

        var result = await _codeExecutionService.ExecuteAsync(request.Language, request.Code, ct);

        return Ok(new RunCodeResponse
        {
            Stdout = result.Stdout,
            Stderr = result.Stderr,
            ExitCode = result.ExitCode,
            TimedOut = result.TimedOut
        });
    }
}
