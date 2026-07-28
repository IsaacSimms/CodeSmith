// == Problem Focus Enum == //
namespace CodeSmith.Core.Enums;

/// <summary>
/// The approach style of a generated coding problem — what kind of work the student actually does.
/// Random is the zero value on purpose: a request that omits the field deserializes to Random and
/// keeps the historical fully-random behavior, so older clients need no changes.
/// </summary>
public enum ProblemFocus
{
    Random = 0,               // Roll a concrete focus — see TutoringPromptTemplates.WeightedFocusRoll
    Standard,                 // Implement the solution from a stub
    BugFix,                   // Starter code hides subtle bugs to find and fix
    PerformanceOptimization,  // A naive solution is given; improve its time or space complexity
    FeatureExtension,         // Working code lacks a feature the student must add
    UnusualConstraints,       // Solve under a restriction — no library methods, single pass, O(1) space
    EdgeCaseGauntlet,         // Design tests that stress boundary conditions and non-obvious inputs
    RealWorldScenario,        // Frame the exercise in a plausible domain rather than an abstract function
    Refactoring               // Working but poorly structured code to improve without changing behavior
}
