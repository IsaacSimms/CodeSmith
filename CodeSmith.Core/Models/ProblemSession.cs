// == Problem Session Model == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Core.Models;

/// <summary>
/// Represents an active coding problem session with full conversation history.
/// </summary>
public class ProblemSession
{
    public Guid SessionId { get; set; } = Guid.NewGuid();          // Unique identifier for this session
    public Difficulty Difficulty { get; set; }                      // The difficulty level of the problem
    public Language Language { get; set; }                          // The programming language for this session
    public string ProblemDescription { get; set; } = string.Empty;  // The problem description presented to the user
    public string StarterCode { get; set; } = string.Empty;         // The starter code template for the problem
    public List<ChatMessage> Messages { get; set; } = [];           // The conversation history for this session
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;     // The UTC timestamp when the session was created
}
