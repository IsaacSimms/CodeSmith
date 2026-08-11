// == Prompt Lab Session Model == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;

namespace CodeSmith.Core.Models.PromptLab;

/// <summary>
/// Represents an active Prompt Lab session for a user working on a specific challenge.
/// </summary>
public class PromptLabSession : IGuidanceSession
{
    public Guid SessionId { get; set; } = Guid.NewGuid();               // Unique session identifier
    public string ChallengeId { get; set; } = string.Empty;             // The challenge this session is for
    public AiProvider Provider { get; set; } = AiProvider.Anthropic;    // AI provider locked at session start
    public List<TestInput> TestInputs { get; set; } = [];               // Dynamically generated inputs for this session
    public bool DynamicInputsGenerated { get; set; }                 // True when LLM generated test inputs; false when static fallback was used
    public List<ChallengeAttempt> Attempts { get; set; } = [];      // History of prompt submissions for this session
    public List<ChatMessage> ChatHistory { get; set; } = [];        // Session-scoped guidance chat; capped at 20 turns before trimming
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;     // UTC timestamp when the session was created

    List<ChatMessage> IGuidanceSession.GuidanceHistory => ChatHistory; // Explicit so serialization doesn't duplicate ChatHistory
}
