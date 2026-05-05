// == Prompt Lab Session Model == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Core.Models.PromptLab;

/// <summary>
/// Represents an active Prompt Lab session for a user working on a specific challenge.
/// </summary>
public class PromptLabSession
{
    public Guid SessionId { get; set; } = Guid.NewGuid();               // Unique session identifier
    public string ChallengeId { get; set; } = string.Empty;             // The challenge this session is for
    public AiProvider Provider { get; set; } = AiProvider.Anthropic;    // AI provider locked at session start
    public List<TestInput> TestInputs { get; set; } = [];               // Dynamically generated inputs for this session
    public List<ChallengeAttempt> Attempts { get; set; } = [];      // History of prompt submissions for this session
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;     // UTC timestamp when the session was created
}
