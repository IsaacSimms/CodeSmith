// == Problem Generator Interface == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Generates a complete coding problem (description + starter code) from a difficulty and language.
/// Owns prompt construction, LLM calls, response parsing, and parse-retry behind a single deep seam.
/// </summary>
public interface IProblemGenerator
{
    Task<(string Description, string StarterCode)> GenerateAsync(
        Difficulty difficulty, Language language, AiProvider provider, CancellationToken ct = default);

    // Streaming sibling of GenerateAsync: only the DESCRIPTION portion streams through
    // onDescriptionDelta as it is written (starter code needs the full text to parse reliably, so
    // it arrives only in the returned tuple); onReset fires before each retry attempt so consumers
    // can clear description text already shown from a failed attempt.
    Task<(string Description, string StarterCode)> StreamGenerateAsync(
        Difficulty difficulty,
        Language language,
        AiProvider provider,
        Func<string, CancellationToken, Task> onDescriptionDelta,
        Func<CancellationToken, Task> onReset,
        CancellationToken ct = default);
}
