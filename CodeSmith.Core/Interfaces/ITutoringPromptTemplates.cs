// == Tutoring Prompt Templates Interface == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Models;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Owns prompt construction, variety selection, and LLM response parsing for the tutoring feature.
/// Separates prompt concerns from orchestration so templates are independently testable and tunable.
/// </summary>
public interface ITutoringPromptTemplates
{
    // Resolves any Random focus/topic on the spec, then builds the system prompt and user message
    ProblemGenerationRequest ProblemGeneration(ProblemSpec spec);

    // Builds the system prompt for a guidance or code-analysis turn, optionally appending the editor
    // snapshot. A Random focus means "unspecified" and omits the focus statement entirely.
    string GuidanceSystemPrompt(Language language, string problemDescription, string starterCode,
                                string? editorContent = null, GuidanceMode guidanceMode = GuidanceMode.Guidance,
                                ProblemFocus focus = ProblemFocus.Random);

}

// Carries everything the generator needs from a single ProblemGeneration call. Focus and Topic are
// the post-roll resolved values — never Random — so the caller can log them and store them on the session.
public record ProblemGenerationRequest(
    string       SystemPrompt,
    string       UserMessage,
    ProblemFocus Focus,
    ProblemTopic Topic,
    string       LanguageLabel);
