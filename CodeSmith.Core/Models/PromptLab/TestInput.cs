// == Test Input Model == //
namespace CodeSmith.Core.Models.PromptLab;

/// <summary>
/// A single test case within a challenge's test suite.
/// ExpectedBehavior is for the evaluator only — never exposed to the client.
/// </summary>
public class TestInput
{
    public string InputId { get; set; } = string.Empty;           // Stable identifier, e.g. "input-1"
    public string Label { get; set; } = string.Empty;              // User-visible label, e.g. "Edge case: empty list"
    public string UserMessage { get; set; } = string.Empty;        // Message sent to the simulated model for this test
    public string ExpectedBehavior { get; set; } = string.Empty;   // NEVER expose to client — describes the ideal output for the evaluator
}
