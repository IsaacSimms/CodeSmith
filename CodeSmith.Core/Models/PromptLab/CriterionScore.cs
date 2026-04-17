// == Criterion Score Model == //
namespace CodeSmith.Core.Models.PromptLab;

/// <summary>
/// Records how many points were awarded for a single rubric criterion on one test input.
/// </summary>
public class CriterionScore
{
    public string CriterionId { get; set; } = string.Empty;    // Matches RubricCriterion.CriterionId
    public string CriterionName { get; set; } = string.Empty;  // Display name for the criterion
    public int Points { get; set; }                              // Points awarded (0 to MaxPoints)
    public int MaxPoints { get; set; }                           // Maximum available for this criterion
}
