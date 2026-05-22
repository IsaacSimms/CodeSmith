// == System Lab Attempt Result Response DTO == //
using CodeSmith.Core.Models.PromptLab;
using CodeSmith.Core.Models.SystemLab;

namespace CodeSmith.Api.DTOs.SystemLab;

/// <summary>
/// Client-facing result for a scenario attempt, including rubric breakdown,
/// tradeoff engagement results, and per-dimension deductions.
/// </summary>
public class SystemLabAttemptResultResponse
{
    public Guid AttemptId { get; set; }
    public int RubricScore { get; set; }
    public int MaxRubricScore { get; set; }
    public List<DimensionDeductionDto> DimensionDeductions { get; set; } = [];
    public int TotalScore { get; set; }
    public int MaxScore { get; set; }
    public string OverallFeedback { get; set; } = string.Empty;
    public List<SystemLabCriterionScoreDto> CriterionScores { get; set; } = [];
    public List<TradeoffResultDto> TradeoffResults { get; set; } = [];
    public DateTime SubmittedAt { get; set; }

    public static SystemLabAttemptResultResponse FromAttempt(ScenarioAttempt attempt) => new()
    {
        AttemptId          = attempt.AttemptId,
        RubricScore        = attempt.RubricScore,
        MaxRubricScore     = attempt.MaxRubricScore,
        DimensionDeductions = attempt.DimensionDeductions.Select(DimensionDeductionDto.From).ToList(),
        TotalScore         = attempt.TotalScore,
        MaxScore           = attempt.MaxScore,
        OverallFeedback    = attempt.OverallFeedback,
        CriterionScores    = attempt.CriterionScores.Select(SystemLabCriterionScoreDto.From).ToList(),
        TradeoffResults    = attempt.TradeoffResults.Select(TradeoffResultDto.From).ToList(),
        SubmittedAt        = attempt.SubmittedAt
    };
}

/// <summary>Per-dimension deduction result within an attempt.</summary>
public class DimensionDeductionDto
{
    public string DimensionName { get; set; } = string.Empty;
    public int Deduction { get; set; }
    public string? Feedback { get; set; }

    public static DimensionDeductionDto From(DimensionDeduction d) => new()
    {
        DimensionName = d.DimensionName,
        Deduction     = d.Deduction,
        Feedback      = d.Feedback
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
