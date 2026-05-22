// == Cross-Cutting Dimension Model == //
namespace CodeSmith.Core.Models.SystemLab;

public class CrossCuttingDimension
{
    public string Name { get; set; } = string.Empty;       // "Security", "Cost Awareness"
    public List<string> Pitfalls { get; set; } = [];       // NEVER expose to client — evaluator checks these for deductions
    public int MaxDeduction { get; set; }                  // Cap on total deduction for this dimension
}
