// == Problem Topic Enum == //
namespace CodeSmith.Core.Enums;

/// <summary>
/// The subject area of a generated coding problem — what the problem is about, as distinct from
/// ProblemFocus, which is what kind of work it asks for. Random is the zero value for the same
/// backward-compatibility reason as ProblemFocus.
/// </summary>
public enum ProblemTopic
{
    Random = 0,                     // Roll a concrete topic — see TutoringPromptTemplates.TopicRoll
    ArraysAndStrings,
    HashMapsAndSets,
    TreesAndGraphs,
    DynamicProgramming,
    ObjectOrientedDesign,
    FunctionalPatternsAndRecursion,
    SimulationAndModeling,
    MathAndNumberTheory,
    StateMachines,
    ParsingAndStringProcessing,
    BitManipulation,
    SortingAndSearching
}
