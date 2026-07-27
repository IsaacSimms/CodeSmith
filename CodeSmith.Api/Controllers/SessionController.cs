// == Session Controller == //
using CodeSmith.Api.Authorization;
using CodeSmith.Api.DTOs;
using CodeSmith.Api.Streaming;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authorization;
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
    private readonly AiOptions _aiOptions;

    public SessionController(
        ITutoringService tutoringService,
        IOptions<AiOptions> aiOptions)
    {
        _tutoringService = tutoringService;
        _aiOptions       = aiOptions.Value;
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
    [MeteredAi]
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

        if (!Enum.IsDefined(typeof(AiProvider), request.Provider))
        {
            return BadRequest(new { error = "Invalid provider value. Use Anthropic, OpenAi, or Xai." });
        }

        var session = await _tutoringService.GenerateProblemAsync(request.Difficulty, request.Language, request.Provider, ct);

        return CreatedAtAction(nameof(CreateSession), new { sessionId = session.SessionId }, session);
    }

    // == Create Session Stream Endpoint == //

    [HttpPost("session/stream")]  // NDJSON sibling of CreateSession: description deltas stream, the full session rides the final event
    [MeteredAi]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSessionStream(
        [FromBody] CreateSessionRequest request,
        CancellationToken ct)
    {
        // Same validations as the blocking sibling — these run before any write, so they keep real 400s
        if (!Enum.IsDefined(typeof(Difficulty), request.Difficulty))
            return BadRequest(new { error = "Invalid difficulty value. Use Easy, Medium, or Hard." });
        if (!Enum.IsDefined(typeof(Language), request.Language))
            return BadRequest(new { error = "Invalid language value. Use CSharp, Cpp, Go, Rust, Python, Java, or TypeScript." });
        if (!Enum.IsDefined(typeof(AiProvider), request.Provider))
            return BadRequest(new { error = "Invalid provider value. Use Anthropic, OpenAi, or Xai." });

        var writer = new NdjsonStreamWriter(Response);
        try
        {
            var session = await _tutoringService.StreamGenerateProblemAsync(
                request.Difficulty, request.Language, request.Provider,
                writer.WriteDeltaAsync,
                writer.WriteResetAsync,
                ct);
            await writer.WriteFinalAsync(session, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Client gone — nothing to write and nobody to receive it
        }
        catch (Exception ex) when (Response.HasStarted)
        {
            // Status line is frozen once deltas were written; the failure must ride the stream
            await writer.WriteErrorAsync(ex);
        }
        // Pre-stream failures (402 quota, 429, 502 before the first delta) propagate to
        // AppExceptionHandler while the status line is still writable
        return new EmptyResult();   // body was written directly; nothing for MVC to execute
    }

    // == Chat Endpoint == //

    [HttpPost("session/{sessionId:guid}/chat")]  // Sends a message within an existing session and receives guided assistance
    [MeteredAi]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Chat(
        Guid sessionId,
        [FromBody] ChatRequest request,
        CancellationToken ct)
    {
        var response = await _tutoringService.GetGuidanceAsync(sessionId, request.Message, request.EditorContent, request.GuidanceMode, ct);

        return Ok(response);
    }

    // == Chat Stream Endpoint == //

    [HttpPost("session/{sessionId:guid}/chat/stream")]  // NDJSON sibling of Chat: reply deltas stream, ChatResponse metadata rides the final event
    [MeteredAi]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChatStream(
        Guid sessionId,
        [FromBody] ChatRequest request,
        CancellationToken ct)
    {
        var writer = new NdjsonStreamWriter(Response);
        try
        {
            var response = await _tutoringService.StreamGuidanceAsync(
                sessionId, request.Message, request.EditorContent, request.GuidanceMode,
                writer.WriteDeltaAsync, ct);
            await writer.WriteFinalAsync(response, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Client gone — nothing to write and nobody to receive it
        }
        catch (Exception ex) when (Response.HasStarted)
        {
            // Status line is frozen once deltas were written; the failure must ride the stream
            await writer.WriteErrorAsync(ex);
        }
        // Pre-stream failures (402 quota, 404 session, 429, 502 before the first delta) propagate
        // to AppExceptionHandler while the status line is still writable
        return new EmptyResult();
    }

    // == Run Code Endpoint == //

    // Authenticated but deliberately NOT [MeteredAi] — a code run costs sandbox CPU, not LLM tokens,
    // so it is never debited against quota or credits. [Authorize] exists purely to stop anonymous
    // callers from driving sandbox scale-out (and the resulting compute bill) with a leaked sessionId.
    [HttpPost("session/{sessionId:guid}/run")]  // Executes user code in a sandboxed process with a 10-second timeout
    [Authorize]
    [ProducesResponseType(typeof(RunCodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RunCode(
        Guid sessionId,
        [FromBody] RunCodeRequest request,
        CancellationToken ct)
    {
        var result = await _tutoringService.RunCodeAsync(sessionId, request.Language, request.Code, ct);

        return Ok(new RunCodeResponse
        {
            Stdout = result.Stdout,
            Stderr = result.Stderr,
            ExitCode = result.ExitCode,
            TimedOut = result.TimedOut
        });
    }
}
