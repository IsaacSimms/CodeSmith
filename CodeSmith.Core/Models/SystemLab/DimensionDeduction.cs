// == Dimension Deduction Model == //
namespace CodeSmith.Core.Models.SystemLab;

public class DimensionDeduction
{
    public string DimensionName { get; set; } = string.Empty;  // Matches CrossCuttingDimension.Name
    public int Deduction { get; set; }                          // 0 if no pitfall was triggered
    public string? Feedback { get; set; }                       // null when Deduction is 0
}
