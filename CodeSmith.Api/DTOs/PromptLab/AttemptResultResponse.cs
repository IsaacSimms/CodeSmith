// == Attempt Result Response DTO == //
using CodeSmith.Core.Models.PromptLab;

namespace CodeSmith.Api.DTOs.PromptLab;

/// <summary>
/// Client-facing result for a prompt attempt, including per-input pass/fail and scores.
/// </summary>
public class AttemptResultResponse
{
    public Guid AttemptId { get; set; }
    public int TotalScore { get; set; }
    public int MaxScore { get; set; }
    public string OverallFeedback { get; set; } = string.Empty;
    public List<TestInputResultDto> Results { get; set; } = [];
    public DateTime SubmittedAt { get; set; }

    public static AttemptResultResponse FromAttempt(ChallengeAttempt attempt) => new()
    {
        AttemptId       = attempt.AttemptId,
        TotalScore      = attempt.TotalScore,
        MaxScore        = attempt.MaxScore,
        OverallFeedback = attempt.OverallFeedback,
        Results         = attempt.Results.Select(TestInputResultDto.From).ToList(),
        SubmittedAt     = attempt.SubmittedAt
    };
}

/// <summary>Per-input result including simulation output and criterion breakdown.</summary>
public class TestInputResultDto
{
    public string InputId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string SimulationOutput { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public List<CriterionScoreDto> CriterionScores { get; set; } = [];
    public string Feedback { get; set; } = string.Empty;

    public static TestInputResultDto From(TestInputResult result) => new()
    {
        InputId          = result.InputId,
        Label            = result.Label,
        SimulationOutput = result.SimulationOutput,
        Passed           = result.Passed,
        CriterionScores  = result.CriterionScores.Select(CriterionScoreDto.From).ToList(),
        Feedback         = result.Feedback
    };
}

/// <summary>Per-criterion score within a test input result.</summary>
public class CriterionScoreDto
{
    public string CriterionId { get; set; } = string.Empty;
    public string CriterionName { get; set; } = string.Empty;
    public int Points { get; set; }
    public int MaxPoints { get; set; }

    public static CriterionScoreDto From(CriterionScore score) => new()
    {
        CriterionId   = score.CriterionId,
        CriterionName = score.CriterionName,
        Points        = score.Points,
        MaxPoints     = score.MaxPoints
    };
}
