// == GuidanceMode Enum == //
namespace CodeSmith.Core.Enums;

/// <summary>
/// Discriminates between a standard tutoring turn and a post-execution code analysis turn.
/// </summary>
public enum GuidanceMode
{
    Guidance,     // Standard hint-driven tutoring — guide toward the solution with leading questions
    CodeAnalysis  // Post-execution analysis — interpret run results and nudge toward the fix
}
