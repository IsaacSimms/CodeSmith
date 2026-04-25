// == Chat Response Model == //
namespace CodeSmith.Core.Models;

/// <summary>
/// Response model containing the assistant's reply and context window usage for the current turn.
/// </summary>
public class ChatResponse
{
    public string Response { get; set; } = string.Empty;       // The assistant's response text
    public int ContextTokensUsed { get; set; }                 // Input tokens consumed this turn (system prompt + full history + new message)
    public int ContextWindowSize { get; set; } = 200_000;      // Model context window limit in tokens
}
