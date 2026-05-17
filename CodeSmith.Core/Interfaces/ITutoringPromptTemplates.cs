// == Tutoring Prompt Templates Interface == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Owns prompt construction, variety selection, and LLM response parsing for the tutoring feature.
/// Separates prompt concerns from orchestration so templates are independently testable and tunable.
/// </summary>
public interface ITutoringPromptTemplates
{
    // Selects a random category and angle, then builds the system prompt and user message for problem generation
    ProblemGenerationRequest ProblemGeneration(Difficulty difficulty, Language language);

    // Builds the system prompt for a guidance or code-analysis turn, optionally appending the editor snapshot
    string GuidanceSystemPrompt(Language language, string problemDescription, string starterCode,
                                string? editorContent = null, bool isCodeAnalysis = false);

}

// Carries everything TutoringService needs from a single ProblemGeneration call,
// including Category and Angle so the caller can log what was selected.
public record ProblemGenerationRequest(
    string SystemPrompt,
    string UserMessage,
    string Category,
    string Angle,
    string LanguageLabel);
