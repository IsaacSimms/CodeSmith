// == Chat Response Model == //
namespace CodeSmith.Core.Models;

/// <summary>
/// Response model containing the assistant's reply to a chat message.
/// </summary>
public class ChatResponse
{
    public string Response { get; set; } = string.Empty;  // The assistant's response text
}
