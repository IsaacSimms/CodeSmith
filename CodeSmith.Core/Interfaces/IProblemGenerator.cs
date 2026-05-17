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
}
