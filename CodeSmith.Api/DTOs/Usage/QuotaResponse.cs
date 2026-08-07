// == Quota Response DTO == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Api.DTOs.Usage;

/// <summary>
/// Customer-facing free-quota snapshot. Remaining is <c>freeQuotaMax − freeTokensUsed</c>;
/// <see cref="IpConstraint"/> is a three-state enum so a pollable endpoint never meters co-tenants.
/// </summary>
public class QuotaResponse
{
    public long FreeTokensUsed { get; set; }     // Tokens consumed against the account free grant
    public long FreeQuotaMax { get; set; }       // One-time grant size (per-row snapshot, or config when no row)
    public IpConstraint IpConstraint { get; set; } // None | Limited | Exhausted — never a raw IP count
}
