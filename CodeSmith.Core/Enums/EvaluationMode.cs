// == Evaluation Mode Enum == //
namespace CodeSmith.Core.Enums;

/// <summary>
/// Controls how the evaluator scores a System Lab justification.
/// The evaluation prompt is constructed differently per mode.
/// </summary>
public enum EvaluationMode
{
    SingleAnswer,       // Easy: one correct choice; rubric rewards correctness
    TradeoffReasoning,  // Medium: defensible choice weighted by an identified constraint; rubric rewards tradeoff engagement
    OpenJudgment        // Hard: multiple valid designs; rubric rewards the quality of the reasoning process, not the choice
}
