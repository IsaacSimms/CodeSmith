// == System Lab Attempt Result Response DTO == //
using CodeSmith.Core.Models.PromptLab;
using CodeSmith.Core.Models.SystemLab;

namespace CodeSmith.Api.DTOs.SystemLab;

/// <summary>
/// Client-facing result for a scenario attempt, including rubric breakdown,
/// tradeoff engagement results, and any security deduction.
/// </summary>
public class SystemLabAttemptResultResponse
{
    public Guid AttemptId { get; set; }
    public int RubricScore { get; set; }
    public int MaxRubricScore { get; set; }
    public int SecurityDeduction { get; set; }
    public int TotalScore { get; set; }
    public int MaxScore { get; set; }
    public string OverallFeedback { get; set; } = string.Empty;
    public string? SecurityFeedback { get; set; }
    public List<SystemLabCriterionScoreDto> CriterionScores { get; set; } = [];
    public List<TradeoffResultDto> TradeoffResults { get; set; } = [];
    public DateTime SubmittedAt { get; set; }

    public static SystemLabAttemptResultResponse FromAttempt(ScenarioAttempt attempt) => new()
    {
        AttemptId         = attempt.AttemptId,
        RubricScore       = attempt.RubricScore,
        MaxRubricScore    = attempt.MaxRubricScore,
        SecurityDeduction = attempt.SecurityDeduction,
        TotalScore        = attempt.TotalScore,
        MaxScore          = attempt.MaxScore,
        OverallFeedback   = attempt.OverallFeedback,
        SecurityFeedback  = attempt.SecurityFeedback,
        CriterionScores   = attempt.CriterionScores.Select(SystemLabCriterionScoreDto.From).ToList(),
        TradeoffResults   = attempt.TradeoffResults.Select(TradeoffResultDto.From).ToList(),
        SubmittedAt       = attempt.SubmittedAt
    };
}

/// <summary>Per-criterion score within a scenario attempt result.</summary>
public class SystemLabCriterionScoreDto
{
    public string CriterionId { get; set; } = string.Empty;
    public string CriterionName { get; set; } = string.Empty;
    public int Points { get; set; }
    public int MaxPoints { get; set; }

    public static SystemLabCriterionScoreDto From(CriterionScore score) => new()
    {
        CriterionId   = score.CriterionId,
        CriterionName = score.CriterionName,
        Points        = score.Points,
        MaxPoints     = score.MaxPoints
    };
}

/// <summary>Per-tradeoff engagement result.</summary>
public class TradeoffResultDto
{
    public string TradeoffQuestion { get; set; } = string.Empty;
    public bool Engaged { get; set; }
    public string Feedback { get; set; } = string.Empty;

    public static TradeoffResultDto From(TradeoffResult result) => new()
    {
        TradeoffQuestion = result.TradeoffQuestion,
        Engaged          = result.Engaged,
        Feedback         = result.Feedback
    };
}
