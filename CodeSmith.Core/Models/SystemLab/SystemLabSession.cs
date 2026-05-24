// == System Lab Session Model == //
namespace CodeSmith.Core.Models.SystemLab;

using CodeSmith.Core.Enums;
using CodeSmith.Core.Models;

public class SystemLabSession
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public string ScenarioId { get; set; } = string.Empty;
    public AiProvider Provider { get; set; } = AiProvider.Anthropic;
    public List<ScenarioAttempt> Attempts { get; set; } = [];
    public List<ChatMessage> ChatHistory { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
