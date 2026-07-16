// == Usage Snapshot Model == //
namespace CodeSmith.Core.Models.Usage;

/// <summary>
/// The complete decision state one enforcement phase needs, read in a single store call:
/// the user's credit balance (null when the objectId has never been persisted) and the free
/// tokens already issued to the client IP across all objectIds.
/// </summary>
public sealed record UsageSnapshot(CreditBalance? Balance, long IpFreeTokensIssued);
