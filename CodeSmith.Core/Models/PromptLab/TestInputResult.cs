// == Test Input Result Model == //
namespace CodeSmith.Core.Models.PromptLab;

/// <summary>
/// The evaluation result for a single test input within a challenge attempt.
/// </summary>
public class TestInputResult
{
    public string InputId { get; set; } = string.Empty;              // Matches TestInput.InputId
    public string Label { get; set; } = string.Empty;                 // Display label for the test
    public string SimulationOutput { get; set; } = string.Empty;      // Raw output from the simulated model
    public bool Passed { get; set; }                                   // True if all rubric criteria were met
    public List<CriterionScore> CriterionScores { get; set; } = [];   // Per-criterion breakdown
    public string Feedback { get; set; } = string.Empty;              // Evaluator's plain-text feedback for this input
}
