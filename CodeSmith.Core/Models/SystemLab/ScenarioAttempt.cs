// == Scenario Attempt Model == //
using CodeSmith.Core.Models.PromptLab;

namespace CodeSmith.Core.Models.SystemLab;

public class ScenarioAttempt
{
    public Guid AttemptId { get; set; } = Guid.NewGuid();
    public string JustificationContent { get; set; } = string.Empty;             // The prose the user submitted
    public List<CriterionScore> CriterionScores { get; set; } = [];              // Per-rubric-criterion breakdown
    public int RubricScore { get; set; }                                          // Sum of criterion points earned
    public int MaxRubricScore { get; set; }                                       // Sum of all criterion MaxPoints
    public List<DimensionDeduction> DimensionDeductions { get; set; } = [];      // Per-dimension deduction results
    public int TotalScore { get; set; }                                           // RubricScore - sum(DimensionDeductions)
    public int MaxScore { get; set; }                                             // MaxRubricScore
    public string OverallFeedback { get; set; } = string.Empty;
    public List<TradeoffResult> TradeoffResults { get; set; } = [];
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}
