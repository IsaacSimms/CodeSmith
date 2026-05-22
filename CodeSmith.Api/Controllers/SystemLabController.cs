// == System Lab Controller == //
using CodeSmith.Api.DTOs.SystemLab;
using CodeSmith.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CodeSmith.Api.Controllers;

/// <summary>
/// Handles System Lab scenario browsing, session management, attempt submission, and guidance chat.
/// </summary>
[ApiController]
[Route("api/system-lab")]
public class SystemLabController : ControllerBase
{
    private readonly ISystemLabService _service;

    public SystemLabController(ISystemLabService service)
    {
        _service = service;
    }

    // == Get All Scenarios Endpoint == //

    [HttpGet("scenarios")]  // Returns the full scenario catalog — SecurityPitfalls are stripped from the response
    [ProducesResponseType(typeof(List<ScenarioResponse>), StatusCodes.Status200OK)]
    public IActionResult GetScenarios()
    {
        var responses = _service.GetScenarios()
            .Select(ScenarioResponse.FromScenario)
            .ToList();

        return Ok(responses);
    }

    // == Get Single Scenario Endpoint == //

    [HttpGet("scenarios/{scenarioId}")]
    [ProducesResponseType(typeof(ScenarioResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetScenario(string scenarioId)
    {
        var scenario = _service.GetScenario(scenarioId); // Throws ScenarioNotFoundException → 404
        return Ok(ScenarioResponse.FromScenario(scenario));
    }

    // == Start Session Endpoint == //

    [HttpPost("sessions")]
    [ProducesResponseType(typeof(SystemLabSessionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartSession([FromBody] StartSystemLabSessionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ScenarioId))
            return BadRequest(new { error = "ScenarioId is required." });

        var session = await _service.StartSessionAsync(request.ScenarioId, ct); // Throws ScenarioNotFoundException → 404
        return CreatedAtAction(nameof(StartSession), new { sessionId = session.SessionId }, SystemLabSessionResponse.FromSession(session));
    }

    // == Submit Attempt Endpoint == //

    [HttpPost("sessions/{sessionId:guid}/submit")]
    [ProducesResponseType(typeof(SystemLabAttemptResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitAttempt(
        Guid sessionId,
        [FromBody] SubmitJustificationRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.JustificationContent))
            return BadRequest(new { error = "JustificationContent is required." });

        var attempt = await _service.SubmitAttemptAsync(sessionId, request.JustificationContent, ct);
        return Ok(SystemLabAttemptResultResponse.FromAttempt(attempt));
    }

    // == Guidance Chat Endpoint == //

    [HttpPost("sessions/{sessionId:guid}/chat")]
    [ProducesResponseType(typeof(SystemLabChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Chat(
        Guid sessionId,
        [FromBody] SystemLabChatRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message is required." });

        var response = await _service.ChatAsync(sessionId, request.Message, request.CurrentJustification, ct);
        return Ok(new SystemLabChatResponse { Response = response });
    }
}
