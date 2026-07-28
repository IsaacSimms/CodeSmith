// == Problem Generator Interface == //
using CodeSmith.Core.Models;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Generates a complete coding problem (description + starter code) from a ProblemSpec.
/// Owns prompt construction, variety resolution, LLM calls, response parsing, and parse-retry
/// behind a single deep seam.
/// </summary>
public interface IProblemGenerator
{
    Task<GeneratedProblem> GenerateAsync(ProblemSpec spec, CancellationToken ct = default);

    // Streaming sibling of GenerateAsync: only the DESCRIPTION portion streams through
    // onDescriptionDelta as it is written (starter code needs the full text to parse reliably, so
    // it arrives only in the returned value); onReset fires before each retry attempt so consumers
    // can clear description text already shown from a failed attempt.
    Task<GeneratedProblem> StreamGenerateAsync(
        ProblemSpec spec,
        Func<string, CancellationToken, Task> onDescriptionDelta,
        Func<CancellationToken, Task> onReset,
        CancellationToken ct = default);
}
