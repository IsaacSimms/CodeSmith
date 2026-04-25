// == Challenge Attempt Model == //
namespace CodeSmith.Core.Models.PromptLab;

/// <summary>
/// Records a single prompt submission attempt, including simulation outputs and evaluation scores.
/// </summary>
public class ChallengeAttempt
{
    public Guid AttemptId { get; set; } = Guid.NewGuid();               // Unique attempt identifier
    public string SystemPromptContent { get; set; } = string.Empty;      // What the user wrote in the system prompt field
    public string UserMessageContent { get; set; } = string.Empty;       // What the user wrote in the user message field
    public List<TestInputResult> Results { get; set; } = [];             // Per-input pass/fail results
    public int TotalScore { get; set; }                                   // Sum of awarded points across all inputs and criteria
    public int MaxScore { get; set; }                                     // Maximum possible score for this attempt
    public string OverallFeedback { get; set; } = string.Empty;          // Evaluator's summary feedback for the whole attempt
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;         // UTC timestamp of the attempt
    public int PromptTokensUsed { get; set; }                            // Input tokens consumed by one simulation call (representative prompt size)
    public int ContextWindowSize { get; set; } = 200_000;                // Model context window limit in tokens
}
