// == Prompt Lab Controller == //
using CodeSmith.Api.DTOs.PromptLab;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models.PromptLab;
using Microsoft.AspNetCore.Mvc;

namespace CodeSmith.Api.Controllers;

/// <summary>
/// Handles Prompt Lab challenge browsing, session management, and attempt submission.
/// </summary>
[ApiController]
[Route("api/prompt-lab")]
public class PromptLabController : ControllerBase
{
    private readonly IPromptLabService _service;

    public PromptLabController(IPromptLabService service)
    {
        _service = service;
    }

    // == Get All Challenges Endpoint == //

    [HttpGet("challenges")]  // Returns the full challenge catalog — hidden fields are stripped from the response
    [ProducesResponseType(typeof(List<ChallengeResponse>), StatusCodes.Status200OK)]
    public IActionResult GetChallenges()
    {
        var responses = _service.GetChallenges()
            .Select(ChallengeResponse.FromChallenge)
            .ToList();

        return Ok(responses);
    }

    // == Get Single Challenge Endpoint == //

    [HttpGet("challenges/{challengeId}")]  // Returns a single challenge by ID — hidden fields are stripped from the response
    [ProducesResponseType(typeof(ChallengeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetChallenge(string challengeId)
    {
        var challenge = _service.GetChallenge(challengeId); // Throws ChallengeNotFoundException → 404
        return Ok(ChallengeResponse.FromChallenge(challenge));
    }

    // == Start Challenge Endpoint == //

    [HttpPost("sessions")]  // Creates a new Prompt Lab session and generates dynamic test inputs for the challenge
    [ProducesResponseType(typeof(PromptLabSessionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartChallenge([FromBody] StartChallengeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ChallengeId))
            return BadRequest(new { error = "ChallengeId is required." });

        var provider = request.Provider ?? AiProvider.Anthropic;
        var session = await _service.StartChallengeAsync(request.ChallengeId, provider, ct); // Throws ChallengeNotFoundException → 404
        return CreatedAtAction(nameof(StartChallenge), new { sessionId = session.SessionId }, PromptLabSessionResponse.FromSession(session));
    }

    // == Submit Attempt Endpoint == //

    [HttpPost("sessions/{sessionId:guid}/submit")]  // Runs the user's prompt against all test inputs and returns scored results
    [ProducesResponseType(typeof(AttemptResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitAttempt(
        Guid sessionId,
        [FromBody] SubmitAttemptRequest request,
        CancellationToken ct)
    {
        var attempt = await _service.SubmitAttemptAsync(
            sessionId,
            request.SystemPromptContent,
            request.UserMessageContent,
            ct);

        return Ok(AttemptResultResponse.FromAttempt(attempt));
    }
}
