// == Session Controller == //
using CodeSmith.Api.DTOs;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CodeSmith.Api.Controllers;

/// <summary>
/// Handles coding problem session creation, chat interactions, and code execution.
/// </summary>
[ApiController]
[Route("api")]
public class SessionController : ControllerBase
{
    private readonly ITutoringService _tutoringService;
    private readonly ICodeExecutionService _codeExecutionService;
    private readonly ISessionStore _sessionStore;
    private readonly AiOptions _aiOptions;

    public SessionController(
        ITutoringService tutoringService,
        ICodeExecutionService codeExecutionService,
        ISessionStore sessionStore,
        IOptions<AiOptions> aiOptions)
    {
        _tutoringService       = tutoringService;
        _codeExecutionService  = codeExecutionService;
        _sessionStore          = sessionStore;
        _aiOptions             = aiOptions.Value;
    }

    // == Providers Endpoint == //

    [HttpGet("providers")]  // Returns the active provider and the list of all known providers
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetProviders()
    {
        var allProviders = Enum.GetNames<AiProvider>();
        return Ok(new
        {
            activeProvider     = _aiOptions.ActiveProvider,
            availableProviders = allProviders
        });
    }

    // == Create Session Endpoint == //

    [HttpPost("session")]  // Creates a new coding problem session at the specified difficulty level
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

        // Resolve provider: use the request's override if supplied, else fall back to the server's configured default
        var providerName = request.Provider.HasValue
            ? request.Provider.Value.ToString()
            : _aiOptions.ActiveProvider;

        if (!Enum.TryParse<AiProvider>(providerName, ignoreCase: true, out var provider))
            return BadRequest(new { error = $"Unknown AI provider '{providerName}'." });

        var session = await _tutoringService.GenerateProblemAsync(request.Difficulty, request.Language, provider, ct);

        return CreatedAtAction(nameof(CreateSession), new { sessionId = session.SessionId }, session);
    }

    // == Chat Endpoint == //

    [HttpPost("session/{sessionId:guid}/chat")]  // Sends a message within an existing session and receives guided assistance
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Chat(
        Guid sessionId,
        [FromBody] ChatRequest request,
        CancellationToken ct)
    {
        var response = await _tutoringService.GetGuidanceAsync(sessionId, request.Message, request.EditorContent, request.IsCodeAnalysis, ct);

        return Ok(response);
    }

    // == Run Code Endpoint == //

    [HttpPost("session/{sessionId:guid}/run")]  // Executes user code in a sandboxed process with a 10-second timeout
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
