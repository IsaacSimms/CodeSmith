// == System Lab Session Model == //
namespace CodeSmith.Core.Models.SystemLab;

using CodeSmith.Core.Models;

public class SystemLabSession
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public string ScenarioId { get; set; } = string.Empty;
    public List<ScenarioAttempt> Attempts { get; set; } = [];
    public List<ChatMessage> ChatHistory { get; set; } = [];    // Session-scoped guidance chat; capped at 20 turns before trimming
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
