// == Usage Reservation Handle == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Core.Models.Usage;

/// <summary>
/// The hold returned by <c>IUsageEnforcer.ReserveAsync</c>, describing exactly what was debited up
/// front (an upper-bound estimate) so <c>SettleAsync</c> can reconcile it to actuals or
/// <c>ReleaseAsync</c> can refund it. It is the value that crosses the seam between the pre-call
/// reserve and the post-call settle/release, carrying everything those two phases need to reverse the
/// hold without re-reading the original request.
/// </summary>
public sealed record UsageReservation
{
    public required string ObjectId { get; init; }            // The user the hold belongs to
    public required string ClientIp { get; init; }            // Normalized client IP ("unknown" when absent)
    public required AiProvider Provider { get; init; }        // Provider the hold was priced against
    public required long ReservedFreeTokens { get; init; }    // Free tokens held against the window quota + per-IP aggregate
    public required decimal ReservedPaidUsd { get; init; }    // Paid charge held against PaidCreditsBalance

    public bool UsedFree => ReservedFreeTokens > 0;           // Drives the decorator's free-tier model downgrade
}
