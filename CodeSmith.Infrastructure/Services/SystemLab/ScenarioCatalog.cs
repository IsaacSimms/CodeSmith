// == Scenario Catalog == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Models.PromptLab;
using CodeSmith.Core.Models.SystemLab;

namespace CodeSmith.Infrastructure.Services.SystemLab;

/// <summary>
/// Authored, finite catalog of System Lab scenarios.
/// Content is fixed at build time — LLM generation is used offline during authoring only, never at runtime.
/// SecurityPitfalls must never be sent to the client; they are used by the evaluator only.
/// </summary>
public static class ScenarioCatalog
{
    public static readonly IReadOnlyList<Scenario> All = BuildCatalog();

    private static List<Scenario> BuildCatalog() =>
    [
        // == Identity & Governance == //

        new Scenario
        {
            ScenarioId     = "identity-rbac-easy-01",
            Title          = "Storage Account Access for a Web App",
            Category       = SystemLabCategory.IdentityAndGovernance,
            Difficulty     = Difficulty.Easy,
            EvaluationMode = EvaluationMode.SingleAnswer,
            Description    = """
                A single-tenant web application running on Azure App Service needs read-only access
                to blob storage in a storage account. A colleague has suggested assigning the built-in
                Contributor role at the subscription level to keep things simple.
                """,
            Constraints    = """
                - The application must be able to read blobs from one specific container.
                - No other Azure resources should be accessible to the app identity.
                - The solution must support auditability of access.
                """,
            RequiredTradeoffs =
            [
                "Why is assigning Contributor at the subscription level dangerous, and what is the minimum-privilege alternative that satisfies the read requirement?",
                "Why does scope (subscription vs. resource group vs. resource) matter here, and what scope would you choose?"
            ],
            Rubric =
            [
                new RubricCriterion
                {
                    CriterionId = "identity-least-privilege",
                    Name        = "Least Privilege",
                    Description = "Recommends the Storage Blob Data Reader role (or equivalent minimum) scoped to the specific storage account or container, not a broader role or scope.",
                    MaxPoints   = 3
                },
                new RubricCriterion
                {
                    CriterionId = "identity-managed-identity",
                    Name        = "Managed Identity",
                    Description = "Uses a system-assigned or user-assigned managed identity rather than a service principal with a stored secret or connection string.",
                    MaxPoints   = 2
                },
                new RubricCriterion
                {
                    CriterionId = "identity-scope",
                    Name        = "Correct Scope",
                    Description = "Role assignment is scoped to the storage account or container level, not subscription or resource group.",
                    MaxPoints   = 2
                },
                new RubricCriterion
                {
                    CriterionId = "identity-auditability",
                    Name        = "Auditability",
                    Description = "Notes that managed identity assignments are logged in Azure Activity Log / Entra ID audit logs, supporting the auditability constraint.",
                    MaxPoints   = 1
                }
            ],
            SecurityPitfalls =
            [
                "Assigning Owner or Contributor at subscription or resource group scope to the application identity",
                "Using a storage account access key or SAS token stored in application settings instead of managed identity",
                "Granting write or delete permissions when only read is required"
            ],
            MaxSecurityDeduction = 4
        },

        // == Networking & Connectivity == //

        new Scenario
        {
            ScenarioId     = "networking-private-endpoint-med-01",
            Title          = "Securing a PaaS Database from the Public Internet",
            Category       = SystemLabCategory.NetworkingAndConnectivity,
            Difficulty     = Difficulty.Medium,
            EvaluationMode = EvaluationMode.TradeoffReasoning,
            Description    = """
                A .NET API hosted on Azure App Service needs to connect to an Azure SQL Database.
                Currently the database has a public endpoint with an IP firewall rule allowing the
                App Service outbound IPs. Your team lead has asked you to evaluate two options:
                keep the current firewall approach or migrate to a private endpoint.
                """,
            Constraints    = """
                - The API and database are in the same region.
                - The App Service is on a Standard plan (VNet integration is available).
                - A compliance requirement states that database traffic must not traverse the public internet.
                - Latency budget for database calls is under 5ms.
                """,
            RequiredTradeoffs =
            [
                "Why does the IP firewall approach fail to meet the compliance requirement, even with correct IP rules in place?",
                "What additional networking components does a private endpoint require, and what is the operational cost of that complexity?",
                "Given the compliance constraint is non-negotiable, what is the correct choice and what does 'correct' mean in this context?"
            ],
            Rubric =
            [
                new RubricCriterion
                {
                    CriterionId = "networking-private-endpoint-choice",
                    Name        = "Correct Solution Choice",
                    Description = "Selects private endpoint as the solution and correctly identifies that IP firewall rules still expose the database via a public endpoint regardless of which IPs are permitted.",
                    MaxPoints   = 3
                },
                new RubricCriterion
                {
                    CriterionId = "networking-components",
                    Name        = "Required Components",
                    Description = "Identifies the required components: VNet integration on App Service, private endpoint NIC in the VNet, private DNS zone (privatelink.database.windows.net), and disabling public endpoint on the database.",
                    MaxPoints   = 3
                },
                new RubricCriterion
                {
                    CriterionId = "networking-dns",
                    Name        = "DNS Resolution",
                    Description = "Explains that the private DNS zone must be linked to the VNet so that the database FQDN resolves to the private IP, not the public IP.",
                    MaxPoints   = 2
                },
                new RubricCriterion
                {
                    CriterionId = "networking-tradeoff-complexity",
                    Name        = "Complexity Tradeoff",
                    Description = "Acknowledges the added operational complexity (DNS management, NIC lifecycle, VNet dependency) and frames it as the cost of the compliance requirement, not an argument against the solution.",
                    MaxPoints   = 2
                }
            ],
            SecurityPitfalls =
            [
                "Recommending the IP firewall approach as compliant because 'only the correct IPs are allowed'",
                "Forgetting to disable the public endpoint after enabling the private endpoint",
                "Not linking the private DNS zone to the VNet, leaving public DNS resolution active"
            ],
            MaxSecurityDeduction = 4
        },

        // == Resilience & Continuity == //

        new Scenario
        {
            ScenarioId     = "resilience-rpo-rto-hard-01",
            Title          = "Business-Critical API: Region Failure and Recovery Commitment",
            Category       = SystemLabCategory.ResilienceAndContinuity,
            Difficulty     = Difficulty.Hard,
            EvaluationMode = EvaluationMode.OpenJudgment,
            Description    = """
                You are designing the disaster recovery posture for a customer-facing payment API
                that processes transactions. Your stakeholders have told you the business can tolerate
                a maximum of 15 minutes of downtime per incident and can afford to lose no more than
                5 minutes of transaction data. Cost is a concern but is secondary to reliability.
                """,
            Constraints    = """
                - RTO: 15 minutes
                - RPO: 5 minutes
                - The API uses Azure SQL Database for transaction storage and Azure Service Bus for async processing.
                - The team has two engineers on-call rotation.
                - Budget is constrained; the CFO will scrutinize any solution that more than doubles the current infrastructure cost.
                """,
            RequiredTradeoffs =
            [
                "What is the difference between HA (high availability) and DR (disaster recovery), and which of the two does a 15-minute RTO primarily require you to design for?",
                "For the 5-minute RPO on SQL, what replication mode and geo-redundancy configuration does that imply, and what is the cost and latency tradeoff of that choice?",
                "Given a 2-engineer on-call team, how does manual vs. automatic failover change your RTO guarantee, and which is appropriate here?",
                "Where does Service Bus fit in your recovery design — does it need its own geo-recovery configuration, and why or why not?"
            ],
            Rubric =
            [
                new RubricCriterion
                {
                    CriterionId = "resilience-ha-vs-dr",
                    Name        = "HA vs DR Distinction",
                    Description = "Correctly distinguishes HA (surviving zone/node failure within a region) from DR (surviving regional failure). Frames the 15-minute RTO as a DR requirement, not just HA.",
                    MaxPoints   = 2
                },
                new RubricCriterion
                {
                    CriterionId = "resilience-sql-replication",
                    Name        = "SQL RPO Design",
                    Description = "Identifies active geo-replication or failover group with a secondary in another region. Notes that the 5-minute RPO requires near-synchronous replication and explains the latency/consistency tradeoff this imposes on write paths.",
                    MaxPoints   = 3
                },
                new RubricCriterion
                {
                    CriterionId = "resilience-failover-mode",
                    Name        = "Failover Mode Reasoning",
                    Description = "Recommends automatic failover (via failover group policy) given the 2-engineer constraint, and explains why manual failover creates RTO risk with a small on-call team.",
                    MaxPoints   = 2
                },
                new RubricCriterion
                {
                    CriterionId = "resilience-service-bus",
                    Name        = "Service Bus Recovery",
                    Description = "Addresses Service Bus geo-disaster recovery (Premium tier paired namespace) or explains a deliberate decision not to geo-replicate it and how in-flight messages are handled in a failover.",
                    MaxPoints   = 2
                },
                new RubricCriterion
                {
                    CriterionId = "resilience-cost-awareness",
                    Name        = "Cost Framing",
                    Description = "Acknowledges the CFO constraint and reasons about which components are non-negotiable vs. where cost can be trimmed without violating RTO/RPO.",
                    MaxPoints   = 1
                }
            ],
            SecurityPitfalls =
            [
                "Designing a recovery plan that requires manual database promotion steps without acknowledging the RTO risk to a 2-person on-call team",
                "Using LRS (locally redundant storage) for SQL backups when the RPO requires cross-region recoverability"
            ],
            MaxSecurityDeduction = 3
        }
    ];
}
