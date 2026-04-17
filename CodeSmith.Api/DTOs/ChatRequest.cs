// == Chat Request DTO == //
using System.ComponentModel.DataAnnotations;

namespace CodeSmith.Api.DTOs;

/// <summary>
/// Request body for sending a chat message within a session.
/// </summary>
public class ChatRequest
{
    [Required(ErrorMessage = "Message is required.")]
    [StringLength(2000, MinimumLength = 1, ErrorMessage = "Message must be between 1 and 2000 characters.")]
    public string Message { get; set; } = string.Empty;  // The user's message text

    [StringLength(50000, ErrorMessage = "Editor content must not exceed 50000 characters.")]
    public string? EditorContent { get; set; }  // Current contents of the code editor

    public bool IsCodeAnalysis { get; set; }  // True when the message is an auto-generated code execution analysis
}
