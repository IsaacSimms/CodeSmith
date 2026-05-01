// == LLM Response Model == //
namespace CodeSmith.Core.Models;

/// <summary>
/// Provider-agnostic response returned by any ILlmService implementation.
/// </summary>
public class LlmResponse
{
    public string Content { get; set; } = string.Empty;  // The text content of the model's response
    public int InputTokensUsed { get; set; }              // Number of input tokens consumed by this call
    public int ContextWindowSize { get; set; }            // Total context window size for the model used
}
