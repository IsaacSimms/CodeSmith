// == Quota Snapshot Model == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Core.Models.Usage;

/// <summary>
/// Lock-free free-quota read for the account page and nav. Reports persisted free-token state
/// (including in-flight reserve holds) plus a three-state IP constraint — never raw IP remaining.
/// </summary>
public sealed record QuotaSnapshot(long FreeTokensUsed, long FreeQuotaMax, IpConstraint IpConstraint);
