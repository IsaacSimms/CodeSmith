// == Tradeoff Result Model == //
namespace CodeSmith.Core.Models.SystemLab;

public class TradeoffResult
{
    public string TradeoffQuestion { get; set; } = string.Empty; // The authored causal question shown to the user
    public bool Engaged { get; set; }                             // True if evaluator found genuine causal reasoning, not just keyword mention
    public string Feedback { get; set; } = string.Empty;         // Per-tradeoff evaluator commentary
}
