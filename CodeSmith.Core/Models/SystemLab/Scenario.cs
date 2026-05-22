// == Scenario Model == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Models.PromptLab;

namespace CodeSmith.Core.Models.SystemLab;

/// <summary>
/// Represents an authored infrastructure scenario from the System Lab catalog.
/// Dimensions must never be sent to the client — they drive evaluator-only deduction scoring.
/// </summary>
public class Scenario
{
    public string ScenarioId { get; set; } = string.Empty;                 // Stable slug, e.g. "identity-storage-access-easy-01"
    public string Title { get; set; } = string.Empty;                       // Display title
    public string Description { get; set; } = string.Empty;                 // User-facing scenario brief
    public string Constraints { get; set; } = string.Empty;                 // Explicit constraints the design must satisfy
    public SystemLabCategory Category { get; set; }                         // Competency domain
    public Difficulty Difficulty { get; set; }                               // Easy / Medium / Hard
    public EvaluationMode EvaluationMode { get; set; }                      // How the evaluator scores — shapes its prompt
    public List<RubricCriterion> Rubric { get; set; } = [];                 // Scoring criteria
    public List<string> RequiredTradeoffs { get; set; } = [];               // Visible to user; authored as causal questions
    public List<CrossCuttingDimension> Dimensions { get; set; } = [];       // NEVER expose to client — evaluator-only deduction dimensions
}
