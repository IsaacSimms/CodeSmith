// == Rubric Criterion Model == //
namespace CodeSmith.Core.Models.PromptLab;

/// <summary>
/// A single scoring criterion used to evaluate model output against a challenge rubric.
/// </summary>
public class RubricCriterion
{
    public string CriterionId { get; set; } = string.Empty;    // Stable identifier, e.g. "valid-json"
    public string Name { get; set; } = string.Empty;            // Short display name, e.g. "Valid JSON"
    public string Description { get; set; } = string.Empty;     // Detailed evaluation instruction for the evaluator
    public int MaxPoints { get; set; }                           // Maximum points available for this criterion
}
