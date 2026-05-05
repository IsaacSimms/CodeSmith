// == Prompt Lab Session Response DTO == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Models.PromptLab;

namespace CodeSmith.Api.DTOs.PromptLab;

/// <summary>
/// Client-safe session response returned when a new Prompt Lab session is started.
/// Includes dynamically generated test input summaries (labels only — no UserMessage or ExpectedBehavior).
/// </summary>
public class PromptLabSessionResponse
{
    public Guid SessionId { get; set; }
    public string ChallengeId { get; set; } = string.Empty;
    public AiProvider Provider { get; set; } = AiProvider.Anthropic;
    public List<TestInputDto> TestInputs { get; set; } = [];  // Generated inputs for this session — labels only
    public List<object> Attempts { get; set; } = [];          // Empty on creation
    public DateTime CreatedAt { get; set; }

    public static PromptLabSessionResponse FromSession(PromptLabSession session) => new()
    {
        SessionId   = session.SessionId,
        ChallengeId = session.ChallengeId,
        Provider    = session.Provider,
        TestInputs  = session.TestInputs.Select(TestInputDto.From).ToList(),
        Attempts    = [],
        CreatedAt   = session.CreatedAt
    };
}
