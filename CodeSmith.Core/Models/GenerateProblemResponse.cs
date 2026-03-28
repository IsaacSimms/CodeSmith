// == Generate Problem Response Model == //
namespace CodeSmith.Core.Models;

/// <summary>
/// Response model containing a generated coding problem.
/// </summary>
public class GenerateProblemResponse
{
    /// <summary>The problem description.</summary>
    public string ProblemDescription { get; set; } = string.Empty;

    /// <summary>The starter code template.</summary>
    public string StarterCode { get; set; } = string.Empty;
}
