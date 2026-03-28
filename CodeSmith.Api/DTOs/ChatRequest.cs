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
}
