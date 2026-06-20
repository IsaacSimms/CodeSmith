// == IP Free Usage Aggregate (for per-IP 60k cap) == //
using System.ComponentModel.DataAnnotations;

namespace CodeSmith.Core.Models.Usage;

/// <summary>
/// Tracks total free tokens issued to any objectId from a given IP address.
/// Used as an aggregate throttle (60k total per IP) in addition to per-objectId caps.
/// </summary>
public class IpFreeUsage
{
    [Key]
    public string Ip { get; set; } = string.Empty;   // Normalized client IP (or "unknown")

    public long FreeTokensIssued { get; set; }       // Total free tokens granted from this IP across all objectIds

    public DateTime FirstSeenUtc { get; set; } = DateTime.UtcNow;
}