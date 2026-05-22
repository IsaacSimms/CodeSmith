// == System Lab Category Enum == //
namespace CodeSmith.Core.Enums;

/// <summary>
/// Represents the infrastructure competency a System Lab scenario tests.
/// Each category isolates one skill domain; difficulty scales the skill, not the domain.
/// </summary>
public enum SystemLabCategory
{
    IdentityAndGovernance,      // RBAC, least privilege, policy guardrails, naming/tagging, hierarchy
    Compute,                    // VM vs container vs serverless, stateful/stateless placement, scaling units
    Storage,                    // Tier selection, redundancy models, consistency, data persistence
    NetworkingAndConnectivity,  // VNet/subnet design, firewall rules, public vs private endpoints, DNS, hybrid
    ResilienceAndContinuity,    // HA vs DR, backup, RTO/RPO, region/zone redundancy, failover
    MonitoringAndObservability, // Instrumentation, alerting, log aggregation, the diagnostic path
    AutomationAndIaC            // Declarative vs imperative, idempotency, drift, what to codify vs click
}
