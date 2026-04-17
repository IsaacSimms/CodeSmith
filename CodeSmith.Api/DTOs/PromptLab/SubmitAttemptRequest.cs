// == Submit Attempt Request DTO == //
using System.ComponentModel.DataAnnotations;

namespace CodeSmith.Api.DTOs.PromptLab;

/// <summary>
/// Request body for submitting a prompt attempt against a challenge.
/// </summary>
public class SubmitAttemptRequest
{
    [StringLength(5000)] public string SystemPromptContent { get; set; } = string.Empty;  // User's system prompt additions (may be empty)
    [StringLength(5000)] public string UserMessageContent  { get; set; } = string.Empty;  // User's user message — only meaningful when UserMessage is an editable field
}
