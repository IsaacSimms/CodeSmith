// == Scenario Attempt Model == //
using CodeSmith.Core.Models.PromptLab;

namespace CodeSmith.Core.Models.SystemLab;

public class ScenarioAttempt
{
    public Guid AttemptId { get; set; } = Guid.NewGuid();
    public string JustificationContent { get; set; } = string.Empty;       // The prose the user submitted
    public List<CriterionScore> CriterionScores { get; set; } = [];        // Per-rubric-criterion breakdown
    public int RubricScore { get; set; }                                    // Sum of criterion points earned
    public int MaxRubricScore { get; set; }                                 // Sum of all criterion MaxPoints
    public int SecurityDeduction { get; set; }                              // Points deducted for triggered SecurityPitfalls (0 if none)
    public int TotalScore { get; set; }                                     // RubricScore - SecurityDeduction
    public int MaxScore { get; set; }                                       // MaxRubricScore (security has no positive ceiling)
    public string OverallFeedback { get; set; } = string.Empty;
    public string? SecurityFeedback { get; set; }                           // null if no security deduction was applied
    public List<TradeoffResult> TradeoffResults { get; set; } = [];
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}
