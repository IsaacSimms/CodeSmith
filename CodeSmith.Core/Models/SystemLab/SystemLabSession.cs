// == System Lab Session Model == //
namespace CodeSmith.Core.Models.SystemLab;

using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;

public class SystemLabSession : IGuidanceSession
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public string ScenarioId { get; set; } = string.Empty;
    public AiProvider Provider { get; set; } = AiProvider.Anthropic;
    public List<ScenarioAttempt> Attempts { get; set; } = [];
    public List<ChatMessage> ChatHistory { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    List<ChatMessage> IGuidanceSession.GuidanceHistory => ChatHistory; // Explicit so serialization doesn't duplicate ChatHistory
}
