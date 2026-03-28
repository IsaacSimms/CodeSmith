// == Chat Message Model == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Core.Models;

/// <summary>
/// Represents a single message in a chat conversation.
/// </summary>
public class ChatMessage
{
    public MessageRole Role { get; set; }                       // The role of the message sender
    public string Content { get; set; } = string.Empty;           // The text content of the message
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;    // The UTC timestamp when the message was created
}
