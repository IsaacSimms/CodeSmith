// == IP Constraint Enum == //
namespace CodeSmith.Core.Enums;

/// <summary>
/// How the per-IP free-token aggregate constrains the caller's free grant. Crosses the wire as a
/// string enum so a pollable quota endpoint never meters co-tenants' consumption on a shared NAT.
/// </summary>
public enum IpConstraint
{
    None = 0,       // IP headroom is not the binding constraint on free tokens
    Limited = 1,    // IP binds but some free tokens remain available through the IP
    Exhausted = 2   // IP free aggregate is fully spent — no free tokens from this network
}
