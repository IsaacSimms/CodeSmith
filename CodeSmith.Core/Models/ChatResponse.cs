// == Chat Response Model == //
namespace CodeSmith.Core.Models;

/// <summary>
/// Response model containing the assistant's reply to a chat message.
/// </summary>
public class ChatResponse
{
    /// <summary>The assistant's response text.</summary>
    public string Response { get; set; } = string.Empty;
}
