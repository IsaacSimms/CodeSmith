// == Start Challenge Request DTO == //
using System.ComponentModel.DataAnnotations;

namespace CodeSmith.Api.DTOs.PromptLab;

/// <summary>
/// Request body for starting a new Prompt Lab session.
/// </summary>
public class StartChallengeRequest
{
    [Required] public string ChallengeId { get; set; } = string.Empty;  // The challenge to start
}
