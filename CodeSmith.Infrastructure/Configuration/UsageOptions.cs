// == Usage Options (free quota configuration) == //
namespace CodeSmith.Infrastructure.Configuration;

public class UsageOptions
{
    public const string SectionName = "Usage";

    // Size of the one-time free grant seeded onto each new CreditBalance row. Raising it lifts new rows only.
    public long FreeTokenQuota { get; set; } = 20_000;

    // Markup applied to raw provider cost to produce the customer-facing charge (1.0 = pass-through, 2.0 = 100% margin).
    // Only affects the paid-credit path; free quota is token-based and unaffected.
    public decimal PaidMarkupMultiplier { get; set; } = 2.0m;

    /// <summary>
    /// Explicit list of objectIds that are allowed to use the X-Debug-User-Id header bypass.
    /// Empty in production. Only exact matches are honored.
    /// </summary>
    public string[] AllowedDebugObjectIds { get; set; } = Array.Empty<string>();
}
