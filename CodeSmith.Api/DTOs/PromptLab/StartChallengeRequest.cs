// == Start Challenge Request DTO == //
using System.ComponentModel.DataAnnotations;
using CodeSmith.Core.Enums;

namespace CodeSmith.Api.DTOs.PromptLab;

/// <summary>
/// Request body for starting a new Prompt Lab session.
/// </summary>
public class StartChallengeRequest
{
    [Required] public string ChallengeId { get; set; } = string.Empty;  // The challenge to start
    public AiProvider? Provider { get; set; }  // Omitted → AiOptions.ActiveProvider; explicit value is honored
}
