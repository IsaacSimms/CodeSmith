// == System Lab Session Response DTO == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Models.SystemLab;

namespace CodeSmith.Api.DTOs.SystemLab;

public class SystemLabSessionResponse
{
    public Guid SessionId { get; set; }
    public string ScenarioId { get; set; } = string.Empty;
    public AiProvider Provider { get; set; }  // Resolved provider so an omitting client learns what it got
    public List<SystemLabAttemptResultResponse> Attempts { get; set; } = [];
    public DateTime CreatedAt { get; set; }

    public static SystemLabSessionResponse FromSession(SystemLabSession session) => new()
    {
        SessionId  = session.SessionId,
        ScenarioId = session.ScenarioId,
        Provider   = session.Provider,
        Attempts   = session.Attempts.Select(SystemLabAttemptResultResponse.FromAttempt).ToList(),
        CreatedAt  = session.CreatedAt
    };
}
