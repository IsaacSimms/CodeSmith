// == Generate Problem Response Model == //
namespace CodeSmith.Core.Models;

/// <summary>
/// Response model containing a generated coding problem.
/// </summary>
public class GenerateProblemResponse
{
    public string ProblemDescription { get; set; } = string.Empty;  // The problem description
    public string StarterCode { get; set; } = string.Empty;         // The starter code template
}
