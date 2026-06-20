// == Model Tier Enum == //
namespace CodeSmith.Core.Enums;

/// <summary>
/// The quality/cost tier a caller requests for a completion. Each provider adapter maps a tier
/// to one of its configured model names (e.g. Fast → Haiku/gpt-4.1-mini, Accurate → Sonnet/gpt-4.1).
/// Two tiers cover every current call site; add a member here if a third tier is ever needed.
/// </summary>
public enum ModelTier
{
    Fast,      // Latency- and cost-optimised; used for guidance chat and simulation
    Accurate   // Quality-optimised; used for problem generation, evaluation, and test-input generation
}
