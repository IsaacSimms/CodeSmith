// == Problem Spec == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Core.Models;

/// <summary>
/// Everything that describes the problem to generate, bundled so the generation chain carries one
/// parameter rather than five across four interfaces. Focus and Topic default to Random, which keeps
/// callers that omit them on the historical fully-random behavior.
/// </summary>
public record ProblemSpec(
    Difficulty   Difficulty,
    Language     Language,
    AiProvider   Provider,
    ProblemFocus Focus = ProblemFocus.Random,
    ProblemTopic Topic = ProblemTopic.Random);
