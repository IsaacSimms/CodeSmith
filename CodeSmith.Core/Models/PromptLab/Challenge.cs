// == Challenge Model == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Core.Models.PromptLab;

/// <summary>
/// Represents a curated prompt engineering challenge from the catalog.
/// Contains hidden adversarial data that must never be sent to the client.
/// </summary>
public class Challenge
{
    public string ChallengeId { get; set; } = string.Empty;                  // Stable slug, e.g. "format-json-01"
    public string Title { get; set; } = string.Empty;                         // Display title
    public string Description { get; set; } = string.Empty;                   // User-facing challenge brief
    public ChallengeCategory Category { get; set; }                           // Skill category
    public Difficulty Difficulty { get; set; }                                 // Easy / Medium / Hard
    public string LockedSystemPrompt { get; set; } = string.Empty;            // Base system prompt shown to user (read-only)
    public string HiddenAdversarialPrompt { get; set; } = string.Empty;       // NEVER expose to client — injected into simulation only
    public List<EditableField> EditableFields { get; set; } = [];             // Which fields the user may write
    public List<TestInput> TestInputs { get; set; } = [];                     // 3–5 test cases for robustness scoring
    public List<RubricCriterion> Rubric { get; set; } = [];                   // Scoring criteria used during evaluation
}
