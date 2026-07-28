// == Generated Problem == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Core.Models;

/// <summary>
/// The result of one problem generation: the parsed problem plus the focus and topic that were
/// actually requested of the provider. Random resolves to a concrete value before the LLM call, so
/// these are never Random — they are what the session stores and what the UI badges display.
/// </summary>
public record GeneratedProblem(
    string       Description,
    string       StarterCode,
    ProblemFocus Focus,
    ProblemTopic Topic);
