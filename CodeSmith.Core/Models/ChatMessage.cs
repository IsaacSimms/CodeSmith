// == Chat Message Model == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Core.Models;

/// <summary>
/// Represents a single message in a chat conversation.
/// </summary>
public class ChatMessage
{
    /// <summary>The role of the message sender.</summary>
    public MessageRole Role { get; set; }

    /// <summary>The text content of the message.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>The UTC timestamp when the message was created.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
