// == Problem Session Model == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Core.Models;

/// <summary>
/// Represents an active coding problem session with full conversation history.
/// </summary>
public class ProblemSession
{
    /// <summary>Unique identifier for this session.</summary>
    public Guid SessionId { get; set; } = Guid.NewGuid();

    /// <summary>The difficulty level of the problem.</summary>
    public Difficulty Difficulty { get; set; }

    /// <summary>The problem description presented to the user.</summary>
    public string ProblemDescription { get; set; } = string.Empty;

    /// <summary>The starter code template for the problem.</summary>
    public string StarterCode { get; set; } = string.Empty;

    /// <summary>The conversation history for this session.</summary>
    public List<ChatMessage> Messages { get; set; } = [];

    /// <summary>The UTC timestamp when the session was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
