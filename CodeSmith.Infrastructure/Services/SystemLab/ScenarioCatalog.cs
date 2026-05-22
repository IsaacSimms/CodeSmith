// == Scenario Catalog == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Models.PromptLab;
using CodeSmith.Core.Models.SystemLab;

namespace CodeSmith.Infrastructure.Services.SystemLab;

/// <summary>
/// Authored, finite catalog of System Lab scenarios.
/// Content is fixed at build time — LLM generation is used offline during authoring only, never at runtime.
/// Dimensions (cross-cutting pitfalls) must never be sent to the client; they are used by the evaluator only.
/// </summary>
public static class ScenarioCatalog
{
    public static readonly IReadOnlyList<Scenario> All = BuildCatalog();

    private static List<Scenario> BuildCatalog() =>
    [
        // == Identity & Governance == //
        BuildIdentityStorageAccessEasyScenario(),
        BuildIdentityPimJitMedScenario(),
        BuildIdentitySaasIamHardScenario(),

        // == Compute == //
        BuildComputeStatelessApiEasyScenario(),
        BuildComputeGpuBatchMedScenario(),
        BuildComputeSeasonalScaleHardScenario(),

        // == Storage == //
        BuildStorageImageBlobEasyScenario(),
        BuildStorageRedundancyTiersMedScenario(),
        BuildStoragePolyPersistHardScenario(),

        // == Networking & Connectivity == //
        BuildNetworkingKeyVaultEndpointEasyScenario(),
        BuildNetworkingHubSpokeEastWestMedScenario(),
        BuildNetworkingHybridBgpHardScenario(),

        // == Resilience & Continuity == //
        BuildResilienceZonesVsRegionEasyScenario(),
        BuildResilienceTieredDrMedScenario(),
        BuildResilienceActiveActiveHardScenario(),

        // == Monitoring & Observability == //
        BuildMonitoringNewApiEasyScenario(),
        BuildMonitoringAlertNoiseMedScenario(),
        BuildMonitoringDistributedTraceHardScenario(),

        // == Automation & IaC == //
        BuildAutomationScriptVsIacEasyScenario(),
        BuildAutomationDriftReconcileMedScenario(),
        BuildAutomationPipelinePromotionHardScenario(),
    ];

    // ============================================================
    // Identity & Governance
    // ============================================================

    // == Identity Easy: Storage Account Access == //
    private static Scenario BuildIdentityStorageAccessEasyScenario() => new()
    {
        ScenarioId     = "identity-storage-access-easy-01",
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
                Name        = "Least Privilege Role",
                Description = "Recommends Storage Blob Data Reader (or equivalent minimum) rather than Contributor, Owner, or a broader role.",
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
                Description = "Notes that managed identity assignments are logged in Azure Activity Log / Entra ID audit logs, satisfying the auditability constraint.",
                MaxPoints   = 1
            }
        ],
        Dimensions =
        [
            new CrossCuttingDimension
            {
                Name         = "Security",
                Pitfalls     =
                [
                    "Assigning Owner or Contributor at subscription or resource group scope to the application identity",
                    "Using a storage account access key or SAS token stored in application settings instead of managed identity",
                    "Granting write or delete permissions when only read is required"
                ],
                MaxDeduction = 4
            }
        ]
    };

    // == Identity Medium: PIM JIT for Production Database == //
    private static Scenario BuildIdentityPimJitMedScenario() => new()
    {
        ScenarioId     = "identity-pim-jit-med-01",
        Title          = "Privileged Access Management for a Production Database",
        Category       = SystemLabCategory.IdentityAndGovernance,
        Difficulty     = Difficulty.Medium,
        EvaluationMode = EvaluationMode.TradeoffReasoning,
        Description    = """
            Your organization's production Azure SQL database currently has two permanent admin accounts:
            one shared DBA account used by three engineers, and one break-glass emergency account. The
            security team wants to eliminate permanent privileged access and enforce just-in-time (JIT)
            elevation via Azure AD Privileged Identity Management (PIM). The DBA team argues that PIM
            adds operational friction, especially for on-call scenarios across time zones.
            """,
        Constraints    = """
            - SOC 2 Type II audit requires evidence of who accessed the database, when, and why.
            - The database is in production and must not be inaccessible during incidents.
            - DBAs are distributed across three time zones with overlapping business hours.
            - PIM approval workflow requires a second person to approve elevation requests.
            """,
        RequiredTradeoffs =
        [
            "What are the specific audit and blast-radius risks of permanent shared admin accounts, and how does JIT elevation address them?",
            "Given the approval workflow requirement, how do you handle an on-call incident at 3am where the approver is unavailable?",
            "What is the right role-activation window for the PIM assignment, and what does too short vs. too long a window cost you?"
        ],
        Rubric =
        [
            new RubricCriterion
            {
                CriterionId = "identity-pim-blast-radius",
                Name        = "Blast Radius Reduction",
                Description = "Identifies that shared permanent accounts cannot be attributed to an individual and that JIT limits the window of privilege exposure.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "identity-pim-break-glass",
                Name        = "Break-Glass Design",
                Description = "Proposes a break-glass account with no-approval-required elevation (self-approve or pre-approved emergency role) that is separately monitored and audited.",
                MaxPoints   = 2
            },
            new RubricCriterion
            {
                CriterionId = "identity-pim-window",
                Name        = "Activation Window Reasoning",
                Description = "Gives a defensible activation window (e.g., 2–4 hours for incident work) and explains why too short disrupts operations and too long defeats the JIT purpose.",
                MaxPoints   = 2
            },
            new RubricCriterion
            {
                CriterionId = "identity-pim-audit",
                Name        = "Audit Trail",
                Description = "Notes that PIM activation logs include requestor, approver, justification, and timestamp — producing the SOC 2 evidence trail the shared account cannot provide.",
                MaxPoints   = 1
            }
        ],
        Dimensions =
        [
            new CrossCuttingDimension
            {
                Name         = "Security",
                Pitfalls     =
                [
                    "Keeping any permanent shared admin account as the primary access path instead of individual accounts",
                    "Recommending a break-glass account with no additional monitoring or alerting on activation",
                    "Setting PIM activation to require no justification or business reason"
                ],
                MaxDeduction = 3
            }
        ]
    };

    // == Identity Hard: Multi-Service SaaS IAM Design == //
    private static Scenario BuildIdentitySaasIamHardScenario() => new()
    {
        ScenarioId     = "identity-saas-iam-hard-01",
        Title          = "IAM Architecture for a Multi-Team SaaS Platform",
        Category       = SystemLabCategory.IdentityAndGovernance,
        Difficulty     = Difficulty.Hard,
        EvaluationMode = EvaluationMode.OpenJudgment,
        Description    = """
            A B2B SaaS startup is migrating from a single Azure subscription with manually managed
            permissions to a structured IAM model. The platform has five microservices, three internal
            teams (product engineering, platform ops, and finance), and is onboarding its first
            enterprise customer who requires tenant-level RBAC isolation — their security team must
            be able to grant and revoke their own users' access without contacting the startup's ops team.
            """,
        Constraints    = """
            - Each internal team needs access only to the resources they own.
            - Enterprise customers must be able to manage their own user permissions without startup involvement.
            - All privileged actions must be auditable with a clear trail.
            - The startup currently has a single Entra ID tenant; budget does not support a second tenant.
            """,
        RequiredTradeoffs =
        [
            "How do you model team-level access boundaries — resource groups, management groups, custom roles, or Entra ID groups? What is the tradeoff between flexibility and manageability?",
            "How do you give enterprise customers self-service RBAC over their own data without giving them access to other tenants' resources or the startup's control plane?",
            "Where does service-to-service identity live, and how does it differ from human operator identity?"
        ],
        Rubric =
        [
            new RubricCriterion
            {
                CriterionId = "identity-team-boundaries",
                Name        = "Team Access Boundaries",
                Description = "Proposes a coherent model (e.g., resource group per team, Entra ID security groups mapped to roles) and explains why it provides isolation without over-complicating day-to-day operations.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "identity-customer-rbac",
                Name        = "Customer Self-Service RBAC",
                Description = "Identifies a mechanism for per-customer RBAC delegation (e.g., custom role scoped to customer resource group, or application-layer RBAC with Entra ID groups) without exposing cross-tenant data or the control plane.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "identity-service-identity",
                Name        = "Service-to-Service Identity",
                Description = "Distinguishes managed identities (for service-to-service) from human operator accounts, and explains why mixing them creates audit and blast-radius problems.",
                MaxPoints   = 2
            },
            new RubricCriterion
            {
                CriterionId = "identity-audit-governance",
                Name        = "Audit and Governance",
                Description = "Addresses how privileged actions are logged and who reviews them — Entra ID audit logs, PIM for elevated access, or Azure Policy for guardrails.",
                MaxPoints   = 2
            }
        ],
        Dimensions =
        [
            new CrossCuttingDimension
            {
                Name         = "Security",
                Pitfalls     =
                [
                    "Giving enterprise customers Owner or Contributor access at the subscription level",
                    "Using a single shared service principal for all microservice-to-resource communication",
                    "No mechanism to detect or alert on permission changes in customer-facing scopes"
                ],
                MaxDeduction = 4
            }
        ]
    };

    // ============================================================
    // Compute
    // ============================================================

    // == Compute Easy: Stateless REST API Host == //
    private static Scenario BuildComputeStatelessApiEasyScenario() => new()
    {
        ScenarioId     = "compute-stateless-api-easy-01",
        Title          = "Choosing a Compute Host for a Stateless REST API",
        Category       = SystemLabCategory.Compute,
        Difficulty     = Difficulty.Easy,
        EvaluationMode = EvaluationMode.SingleAnswer,
        Description    = """
            A team is deploying a new stateless .NET 8 REST API. Average load is 10 requests/second
            with predictable traffic patterns — no significant spikes. Two colleagues debate the
            compute host: one wants an IaaS virtual machine for "full control," another wants
            Azure Functions because "it's serverless and scales automatically."
            """,
        Constraints    = """
            - The API must be always available (no cold-start latency on first request).
            - Deployment frequency: once or twice per week via CI/CD.
            - The team has no desire to manage OS patches or VM lifecycle.
            - Budget is not the primary concern but should be reasonable.
            """,
        RequiredTradeoffs =
        [
            "Why is a virtual machine the wrong choice here, and what operational overhead does it introduce that the team has explicitly said they don't want?",
            "Why are Azure Functions a poor fit for a continuously-running API with no cold-start tolerance, and what is the right alternative?"
        ],
        Rubric =
        [
            new RubricCriterion
            {
                CriterionId = "compute-host-choice",
                Name        = "Correct Host Selection",
                Description = "Selects Azure App Service (or Container Apps) as the correct host, not a VM or Functions, and correctly explains why each rejected option fails at least one stated constraint.",
                MaxPoints   = 4
            },
            new RubricCriterion
            {
                CriterionId = "compute-managed-platform",
                Name        = "Managed Platform Value",
                Description = "Articulates the key benefit: managed OS/runtime patching, no VM lifecycle management, built-in deployment slots or revision management.",
                MaxPoints   = 2
            },
            new RubricCriterion
            {
                CriterionId = "compute-cold-start",
                Name        = "Cold Start Reasoning",
                Description = "Explains why consumption-tier Functions introduce cold-start latency for a continuously-available service and why App Service (always-on) avoids this.",
                MaxPoints   = 2
            }
        ],
        Dimensions =
        [
            new CrossCuttingDimension
            {
                Name         = "Security",
                Pitfalls     =
                [
                    "Recommending a VM with a public IP and no mention of NSG or bastion host for remote administration",
                    "Deploying the API without any mention of managed identity for downstream service access"
                ],
                MaxDeduction = 2
            },
            new CrossCuttingDimension
            {
                Name         = "Cost Awareness",
                Pitfalls     =
                [
                    "Recommending Premium v3 App Service plan for 10 req/s predictable load when Standard suffices",
                    "Recommending a VM SKU with dedicated cores for a workload that would run comfortably on a shared-tier PaaS host"
                ],
                MaxDeduction = 2
            }
        ]
    };

    // == Compute Medium: GPU Batch Processing Host == //
    private static Scenario BuildComputeGpuBatchMedScenario() => new()
    {
        ScenarioId     = "compute-gpu-batch-med-01",
        Title          = "Compute Host for a GPU-Accelerated Batch Processing Service",
        Category       = SystemLabCategory.Compute,
        Difficulty     = Difficulty.Medium,
        EvaluationMode = EvaluationMode.TradeoffReasoning,
        Description    = """
            A data science team runs ML inference jobs as a background service. Each job processes
            a batch of files from a local scratch directory (not shared storage), requires a GPU,
            and runs for 10–60 minutes. Jobs are triggered by an Azure Service Bus queue.
            One engineer advocates for AKS with GPU node pools for "container portability."
            Another argues for dedicated GPU VMs (NC-series) because of the local disk requirement.
            """,
        Constraints    = """
            - Each job reads input files from a local directory populated at job start; files are not shared between jobs.
            - GPU is required (CUDA workload, cannot run on CPU).
            - Jobs run 8–12 hours per day; the queue is empty outside business hours.
            - The team has no existing Kubernetes expertise.
            """,
        RequiredTradeoffs =
        [
            "The local scratch disk requirement is stateful in nature. How does this interact with container scheduling in AKS, and what would you need to do to make AKS viable?",
            "Given the team has no Kubernetes expertise and jobs run only 8–12 hours/day, what is the total cost and operational complexity tradeoff between AKS with GPU nodes vs. VMs with auto-shutdown?",
            "How does the Service Bus trigger change your scaling strategy for either option?"
        ],
        Rubric =
        [
            new RubricCriterion
            {
                CriterionId = "compute-local-disk-constraint",
                Name        = "Local Disk Constraint Analysis",
                Description = "Correctly identifies that local scratch disk is a stateful placement constraint that complicates container scheduling in AKS (requires hostPath or emptyDir) and explains the failure mode if the pod is rescheduled mid-job.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "compute-operational-complexity",
                Name        = "Operational Complexity Tradeoff",
                Description = "Weighs AKS GPU node pool setup and Kubernetes expertise overhead against VM simplicity, and makes a defensible recommendation given the stated team constraints.",
                MaxPoints   = 2
            },
            new RubricCriterion
            {
                CriterionId = "compute-cost-idle",
                Name        = "Idle Cost Handling",
                Description = "Addresses the 12–16 idle hours per day — either VM auto-shutdown + restart on queue trigger, or AKS node pool scale-to-zero — and evaluates the wake-up latency tradeoff.",
                MaxPoints   = 2
            },
            new RubricCriterion
            {
                CriterionId = "compute-service-bus-scaling",
                Name        = "Queue-Driven Scaling",
                Description = "Explains how Service Bus queue depth can drive scaling decisions (KEDA for AKS, VM scale set custom autoscale, or a simple queue-depth check to spin up a VM).",
                MaxPoints   = 1
            }
        ],
        Dimensions =
        [
            new CrossCuttingDimension
            {
                Name         = "Security",
                Pitfalls     =
                [
                    "Recommending the VM or AKS nodes with a public IP and no mention of private networking or bastion access",
                    "Not addressing how the Service Bus connection is authenticated — assuming connection strings in environment variables instead of managed identity"
                ],
                MaxDeduction = 2
            },
            new CrossCuttingDimension
            {
                Name         = "Cost Awareness",
                Pitfalls     =
                [
                    "Recommending VMs that run 24/7 without auto-shutdown when the workload is idle 12–16 hours per day",
                    "Recommending AKS with GPU nodes that are not set to scale to zero during idle hours"
                ],
                MaxDeduction = 2
            }
        ]
    };

    // == Compute Hard: Seasonal Scale for E-Commerce == //
    private static Scenario BuildComputeSeasonalScaleHardScenario() => new()
    {
        ScenarioId     = "compute-seasonal-scale-hard-01",
        Title          = "Compute Scaling Strategy for a Seasonal Retail Platform",
        Category       = SystemLabCategory.Compute,
        Difficulty     = Difficulty.Hard,
        EvaluationMode = EvaluationMode.OpenJudgment,
        Description    = """
            A retail platform currently runs on Azure App Service Standard plan with no auto-scaling
            configured. Average load is 500 concurrent users. The platform expects 50–100x traffic
            during peak sales events (Black Friday, seasonal sales). Last year's event caused
            significant downtime and cart abandonment because the platform could not handle the load.
            The engineering team has three weeks to prepare for the next event.
            """,
        Constraints    = """
            - The platform is a monolithic .NET API + React frontend served via Azure Static Web Apps.
            - Session state is stored in-process (a known tech debt item).
            - Database is Azure SQL Database Standard tier (20 DTUs).
            - The team has three weeks and limited capacity; no major refactoring is possible.
            - Budget can flex for the event window but should not permanently increase 5x baseline cost.
            """,
        RequiredTradeoffs =
        [
            "In-process session state prevents horizontal scaling. How do you address this constraint within three weeks without a full refactor?",
            "The database is on 20 DTUs. At 50x load, what breaks first and how do you address it given the no-refactoring constraint?",
            "What is the difference between auto-scaling (reacting to load) and pre-scaling (anticipating it) for a known peak event, and which is appropriate here?",
            "How do you validate that your scaling strategy will work before the actual event?"
        ],
        Rubric =
        [
            new RubricCriterion
            {
                CriterionId = "compute-session-state",
                Name        = "Session State Migration",
                Description = "Identifies Azure Cache for Redis (or Azure SQL session provider) as the mechanism to externalize session state and enable horizontal scaling within the three-week window.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "compute-db-scaling",
                Name        = "Database Bottleneck",
                Description = "Identifies the database as the likely bottleneck at 50x load and proposes a concrete mitigation: DTU/vCore upgrade pre-event, read replicas for read-heavy queries, or a caching layer in front of hot paths.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "compute-pre-scaling",
                Name        = "Pre-Scaling vs. Auto-Scaling",
                Description = "Recommends pre-scaling the App Service plan ahead of the known event window rather than relying solely on reactive auto-scaling, and explains why reactive scaling lags behind sudden load spikes.",
                MaxPoints   = 2
            },
            new RubricCriterion
            {
                CriterionId = "compute-load-test",
                Name        = "Validation Strategy",
                Description = "Proposes load testing (e.g., Azure Load Testing or k6) before the event to validate that the scaling strategy works and to identify remaining bottlenecks.",
                MaxPoints   = 2
            }
        ],
        Dimensions =
        [
            new CrossCuttingDimension
            {
                Name         = "Security",
                Pitfalls     =
                [
                    "Recommending disabling security controls (WAF, rate limiting) to reduce latency under load"
                ],
                MaxDeduction = 2
            },
            new CrossCuttingDimension
            {
                Name         = "Cost Awareness",
                Pitfalls     =
                [
                    "Recommending a permanent 5x scale-up that stays after the event rather than scaling back down",
                    "Not addressing scale-down scheduling after the event window ends"
                ],
                MaxDeduction = 2
            }
        ]
    };

    // ============================================================
    // Storage
    // ============================================================

    // == Storage Easy: Choosing Storage for User Images == //
    private static Scenario BuildStorageImageBlobEasyScenario() => new()
    {
        ScenarioId     = "storage-image-blob-easy-01",
        Title          = "Choosing a Storage Service for User Profile Images",
        Category       = SystemLabCategory.Storage,
        Difficulty     = Difficulty.Easy,
        EvaluationMode = EvaluationMode.SingleAnswer,
        Description    = """
            A web application needs to store user profile images (JPG and PNG files, typically
            50–500KB each). Users upload images through the app, and the images are served
            directly in the browser via a URL. A teammate suggests Azure Table Storage because
            "it stores data and should work for images too." Another suggests embedding images
            as base64 strings in an Azure SQL Database column.
            """,
        Constraints    = """
            - Images must be readable directly via a URL (browser-friendly).
            - The application handles up to 10,000 active users with roughly one upload per user per month.
            - Cost should be minimal for this storage tier.
            """,
        RequiredTradeoffs =
        [
            "Why is Azure Table Storage the wrong choice for binary file storage, and what is it actually designed for?",
            "Why does storing binary files in a relational database create problems, and what is the purpose-built alternative?"
        ],
        Rubric =
        [
            new RubricCriterion
            {
                CriterionId = "storage-correct-service",
                Name        = "Correct Service Choice",
                Description = "Selects Azure Blob Storage as the correct service for binary object storage, URL-addressable content, and cost-effective at-rest storage of unstructured files.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "storage-table-wrong",
                Name        = "Table Storage Rejection",
                Description = "Correctly identifies that Table Storage is a key-value store for structured data, not binary objects, and would require serialization workarounds.",
                MaxPoints   = 2
            },
            new RubricCriterion
            {
                CriterionId = "storage-sql-wrong",
                Name        = "SQL BLOB Rejection",
                Description = "Explains that storing binary files in SQL creates unnecessary database load, increases backup size, and circumvents the purpose-built CDN/URL delivery of object storage.",
                MaxPoints   = 2
            },
            new RubricCriterion
            {
                CriterionId = "storage-access-tier",
                Name        = "Access Tier Consideration",
                Description = "Notes that Hot tier is appropriate for frequently accessed profile images (or discusses Cool tier for infrequently accessed content with cost justification).",
                MaxPoints   = 1
            }
        ],
        Dimensions =
        [
            new CrossCuttingDimension
            {
                Name         = "Security",
                Pitfalls     =
                [
                    "Making the Blob container publicly accessible without any access control (anonymous read on entire container rather than individual blobs via SAS or CDN token)",
                    "Recommending storing images in SQL with the storage account key hardcoded in the connection string"
                ],
                MaxDeduction = 3
            },
            new CrossCuttingDimension
            {
                Name         = "Cost Awareness",
                Pitfalls     =
                [
                    "Recommending Premium Blob Storage (SSD-backed) for profile images where Hot LRS is sufficient and dramatically cheaper"
                ],
                MaxDeduction = 2
            }
        ]
    };

    // == Storage Medium: Redundancy Tiers for Different Workloads == //
    private static Scenario BuildStorageRedundancyTiersMedScenario() => new()
    {
        ScenarioId     = "storage-redundancy-tiers-med-01",
        Title          = "Database Redundancy Strategy for Heterogeneous Workloads",
        Category       = SystemLabCategory.Storage,
        Difficulty     = Difficulty.Medium,
        EvaluationMode = EvaluationMode.TradeoffReasoning,
        Description    = """
            An application has two Azure SQL databases: a transactional order database that processes
            live customer orders, and a reporting database that is populated nightly from the
            transactional database via a batch job. Your colleague recommends applying geo-redundant
            storage (GRS) and active geo-replication to both databases for simplicity and consistency.
            """,
        Constraints    = """
            - Transactional database: RTO 15 minutes, RPO 5 minutes. Data loss is unacceptable.
            - Reporting database: RTO 4 hours, RPO 24 hours. A one-day-old report is acceptable during recovery.
            - The application runs in East US. A DR region would be West US.
            - Budget scrutiny is in place; the infrastructure team must justify each cost line.
            """,
        RequiredTradeoffs =
        [
            "What redundancy configuration does the transactional database's 5-minute RPO actually require, and what does it cost in terms of replication overhead and latency?",
            "Does the reporting database need the same redundancy as the transactional database? What is the cost and complexity argument for a simpler configuration?",
            "What is the difference between storage-level redundancy (LRS/ZRS/GRS) and database-level redundancy (geo-replication/failover groups), and which one primarily drives your RTO/RPO?"
        ],
        Rubric =
        [
            new RubricCriterion
            {
                CriterionId = "storage-transactional-config",
                Name        = "Transactional Database Configuration",
                Description = "Selects active geo-replication or auto-failover group for the transactional database and explains that GZRS or GRS alone does not provide a 5-minute RPO — database-level replication is required.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "storage-reporting-config",
                Name        = "Reporting Database Configuration",
                Description = "Argues that the reporting database's 24-hour RPO does not justify active geo-replication — standard geo-redundant backups (GRS on backup storage) or ZRS suffices — and quantifies the cost savings.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "storage-redundancy-distinction",
                Name        = "Redundancy Layer Distinction",
                Description = "Clearly separates storage-level redundancy (protects against hardware/datacenter failure) from database-level replication (protects against data loss with a defined RPO), and identifies which layer is relevant for each requirement.",
                MaxPoints   = 2
            }
        ],
        Dimensions =
        [
            new CrossCuttingDimension
            {
                Name         = "Security",
                Pitfalls     =
                [
                    "Recommending that the reporting database use a publicly accessible secondary without VNet integration or private endpoint",
                    "Not addressing how failover authentication (managed identity or connection string rotation) works after a regional failover"
                ],
                MaxDeduction = 2
            },
            new CrossCuttingDimension
            {
                Name         = "Cost Awareness",
                Pitfalls     =
                [
                    "Applying the same active geo-replication cost to the reporting database when its RPO/RTO does not require it",
                    "Recommending Business Critical or Premium tier for the reporting database when General Purpose supports the stated requirements"
                ],
                MaxDeduction = 3
            }
        ]
    };

    // == Storage Hard: Poly-Persistence for a Financial Platform == //
    private static Scenario BuildStoragePolyPersistHardScenario() => new()
    {
        ScenarioId     = "storage-poly-persist-hard-01",
        Title          = "Storage Architecture for a Financial Platform with Mixed Access Patterns",
        Category       = SystemLabCategory.Storage,
        Difficulty     = Difficulty.Hard,
        EvaluationMode = EvaluationMode.OpenJudgment,
        Description    = """
            A financial platform needs to store three categories of data: transaction records
            (written once, immutable, subject to 7-year compliance retention), user sessions
            (high read/write throughput, 30-minute TTL, no durability requirement), and
            analytics aggregates (batch-updated nightly, read heavily by dashboards throughout
            the day, must be queryable by time range and product category).
            """,
        Constraints    = """
            - Transaction records must be tamper-evident; compliance requires immutability guarantees.
            - Session read latency must be under 5ms (p99).
            - Analytics aggregates are updated at midnight; no real-time ingestion is required.
            - The team must be able to explain each storage choice to an external auditor.
            - Budget is not the primary constraint but each choice must be justified.
            """,
        RequiredTradeoffs =
        [
            "What storage service best satisfies the immutability and long-retention requirements for transaction records, and why is a standard SQL table insufficient?",
            "Why is a relational database a poor fit for session storage at sub-5ms latency, and what is the right alternative?",
            "What are the analytics access patterns (time-range queries, product-category grouping), and how do you match storage to those patterns versus using the same SQL database that holds transactions?"
        ],
        Rubric =
        [
            new RubricCriterion
            {
                CriterionId = "storage-immutable-records",
                Name        = "Immutable Transaction Storage",
                Description = "Selects Azure Blob Storage with immutability policies (WORM) or Azure SQL with ledger tables for tamper-evident, compliance-grade retention. Explains why a standard SQL table without immutability fails the compliance constraint.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "storage-session-store",
                Name        = "Session Storage",
                Description = "Selects Azure Cache for Redis (or equivalent in-memory store) for sub-5ms session reads and explains why the durability tradeoff is acceptable given the 30-minute TTL.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "storage-analytics-store",
                Name        = "Analytics Store",
                Description = "Chooses a store suited to nightly batch ingestion + heavy read queries (e.g., Azure Synapse, Azure SQL with columnstore indexes, or materialized views) and explains why the same OLTP database would degrade under analytical query load.",
                MaxPoints   = 2
            },
            new RubricCriterion
            {
                CriterionId = "storage-justification-clarity",
                Name        = "Auditor-Facing Justification",
                Description = "Each storage choice is framed with a clear reason tied to the access pattern and constraint — not just 'it works' but why it's the right fit.",
                MaxPoints   = 2
            }
        ],
        Dimensions =
        [
            new CrossCuttingDimension
            {
                Name         = "Security",
                Pitfalls     =
                [
                    "Recommending session storage (Redis) for transaction records where data loss on TTL expiry would violate compliance",
                    "Not addressing encryption at rest for transaction records given the compliance context",
                    "Recommending public endpoints for any storage tier in a financial platform without explicit justification"
                ],
                MaxDeduction = 4
            },
            new CrossCuttingDimension
            {
                Name         = "Cost Awareness",
                Pitfalls     =
                [
                    "Recommending a Premium Redis tier for session data when Standard tier's latency characteristics satisfy sub-5ms requirements",
                    "Using the same high-durability OLTP database for analytics workloads that could use a cheaper columnar or serverless analytics tier"
                ],
                MaxDeduction = 2
            }
        ]
    };

    // ============================================================
    // Networking & Connectivity
    // ============================================================

    // == Networking Easy: Key Vault Endpoint Security == //
    private static Scenario BuildNetworkingKeyVaultEndpointEasyScenario() => new()
    {
        ScenarioId     = "networking-keyvault-endpoint-easy-01",
        Title          = "Securing Azure Function Access to Key Vault",
        Category       = SystemLabCategory.NetworkingAndConnectivity,
        Difficulty     = Difficulty.Easy,
        EvaluationMode = EvaluationMode.SingleAnswer,
        Description    = """
            An Azure Function app retrieves database connection strings from Azure Key Vault at startup.
            A colleague configured the Key Vault firewall to allow the Function app's outbound IPs.
            You notice that the Function app is on a Consumption plan and the outbound IP list
            changes when Azure rebalances the underlying compute. Additionally, your security
            team has flagged that Key Vault should not be accessible from the public internet.
            """,
        Constraints    = """
            - Key Vault must not be accessible over the public internet.
            - The Function app needs to read secrets at startup and periodically thereafter.
            - The Function app is on a Consumption plan (no dedicated VNet integration without Premium plan).
            - The team cannot change the Function app plan this sprint.
            """,
        RequiredTradeoffs =
        [
            "Why does IP firewall allowlisting fail for a Consumption plan Function app, and what is the fundamental networking property that makes it unreliable?",
            "Given the constraint that you cannot change the Function plan this sprint, what is the best mitigation available, and what is its security tradeoff compared to a full private endpoint solution?"
        ],
        Rubric =
        [
            new RubricCriterion
            {
                CriterionId = "networking-ip-firewall-failure",
                Name        = "IP Firewall Failure Mode",
                Description = "Correctly identifies that Consumption plan Functions share a dynamic IP pool that changes without notice, making an IP allowlist unreliable and a maintenance burden.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "networking-managed-identity-kv",
                Name        = "Managed Identity + RBAC",
                Description = "Proposes managed identity + Key Vault RBAC (Key Vault Secrets User role) as the identity-based access control that does not depend on network IP.",
                MaxPoints   = 2
            },
            new RubricCriterion
            {
                CriterionId = "networking-sprint-constraint",
                Name        = "Sprint Constraint Reasoning",
                Description = "Acknowledges that a true private endpoint requires Premium plan VNet integration and proposes the managed identity approach as the correct near-term fix, with private endpoint as the eventual target.",
                MaxPoints   = 2
            },
            new RubricCriterion
            {
                CriterionId = "networking-public-endpoint-disable",
                Name        = "Disable Public Access",
                Description = "Notes that Key Vault's public network access should be disabled (or set to selected networks only) even when managed identity provides the access control layer.",
                MaxPoints   = 1
            }
        ],
        Dimensions =
        [
            new CrossCuttingDimension
            {
                Name         = "Security",
                Pitfalls     =
                [
                    "Recommending 'allow all Azure services' on the Key Vault firewall as a fix — this allows any Azure tenant's services, not just your Function app",
                    "Recommending storing the connection string directly in Function app settings to work around the Key Vault access issue",
                    "Leaving Key Vault public network access enabled without any rationale given the explicit security team requirement"
                ],
                MaxDeduction = 4
            }
        ]
    };

    // == Networking Medium: Hub-Spoke East-West Traffic == //
    private static Scenario BuildNetworkingHubSpokeEastWestMedScenario() => new()
    {
        ScenarioId     = "networking-hub-spoke-east-west-med-01",
        Title          = "East-West Traffic Inspection in a Hub-and-Spoke VNet Topology",
        Category       = SystemLabCategory.NetworkingAndConnectivity,
        Difficulty     = Difficulty.Medium,
        EvaluationMode = EvaluationMode.TradeoffReasoning,
        Description    = """
            Your organization uses a hub-and-spoke VNet topology with Azure Firewall in the hub.
            Three spoke VNets contain different workloads: a web tier, an API tier, and a data tier.
            Currently, spokes communicate via the hub, and all traffic passes through Azure Firewall.
            Engineers complain that the hub routing adds 2–3ms of latency to inter-spoke calls.
            They propose adding direct VNet peering between spokes (bypassing the hub) to reduce latency.
            A compliance requirement mandates that all east-west traffic between the web, API, and
            data tiers must be inspected and logged.
            """,
        Constraints    = """
            - Compliance requires full inspection and logging of all inter-tier traffic.
            - Current latency through hub firewall: 2–3ms overhead per request.
            - The API tier makes approximately 500 calls/second to the data tier.
            - Azure Firewall is already deployed and configured with application rules.
            """,
        RequiredTradeoffs =
        [
            "If you add direct VNet peering between spokes, what happens to the compliance requirement for east-west traffic inspection?",
            "What is the concrete latency vs. compliance cost of routing through Azure Firewall? Is 2–3ms overhead a justified cost for this workload?",
            "Are there architecturally sound alternatives that reduce latency without violating the inspection requirement?"
        ],
        Rubric =
        [
            new RubricCriterion
            {
                CriterionId = "networking-compliance-violation",
                Name        = "Compliance Impact of Direct Peering",
                Description = "Clearly states that direct spoke-to-spoke peering bypasses the hub firewall, causing traffic to flow without inspection and violating the compliance requirement.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "networking-latency-justification",
                Name        = "Latency Cost Justification",
                Description = "Evaluates whether 2–3ms is acceptable given the workload — e.g., 2ms on a 500 req/s API is a real but bounded cost — and argues that compliance is non-negotiable regardless.",
                MaxPoints   = 2
            },
            new RubricCriterion
            {
                CriterionId = "networking-alternatives",
                Name        = "Latency Reduction Alternatives",
                Description = "Proposes at least one viable alternative: Azure Firewall Premium (faster policy evaluation), firewall rule optimization, or collocating tiers in the same VNet with NSG segmentation where full firewall inspection is not needed.",
                MaxPoints   = 3
            }
        ],
        Dimensions =
        [
            new CrossCuttingDimension
            {
                Name         = "Security",
                Pitfalls     =
                [
                    "Recommending direct spoke-to-spoke peering as a solution that satisfies the compliance requirement",
                    "Recommending disabling or downgrading Azure Firewall to reduce latency"
                ],
                MaxDeduction = 4
            }
        ]
    };

    // == Networking Hard: Hybrid Connectivity with BGP == //
    private static Scenario BuildNetworkingHybridBgpHardScenario() => new()
    {
        ScenarioId     = "networking-hybrid-bgp-hard-01",
        Title          = "Hybrid Connectivity with BGP and Strict Compliance Requirements",
        Category       = SystemLabCategory.NetworkingAndConnectivity,
        Difficulty     = Difficulty.Hard,
        EvaluationMode = EvaluationMode.OpenJudgment,
        Description    = """
            A financial services company needs to connect their on-premises data center to Azure
            workloads. The network team requires BGP for dynamic route management so that route
            changes on-premises propagate automatically to Azure. A compliance requirement states
            that all traffic between on-premises and Azure must not traverse the public internet.
            The latency SLA for database calls from on-premises to Azure SQL is under 10ms.
            """,
        Constraints    = """
            - BGP is required for dynamic route propagation.
            - Traffic must not travel over the public internet at any point (compliance-mandated).
            - Latency SLA: under 10ms round-trip for on-premises to Azure SQL calls.
            - Budget: substantial but must be justified; CFO will approve anything meeting compliance.
            - Business continuity requires the connectivity to have redundancy — single point of failure is not acceptable.
            """,
        RequiredTradeoffs =
        [
            "Why does a site-to-site VPN Gateway fail to meet the compliance requirement even if BGP is enabled on the gateway?",
            "ExpressRoute provides private connectivity. What are the two redundancy models available, and what does each cost in terms of complexity and budget?",
            "Given the 10ms latency requirement, what does physical distance between the on-premises location and the Azure region mean for your design? What can and cannot be controlled?",
            "Is a VPN Gateway as a failover path alongside ExpressRoute acceptable given the compliance constraint? Why or why not?"
        ],
        Rubric =
        [
            new RubricCriterion
            {
                CriterionId = "networking-expressroute-choice",
                Name        = "ExpressRoute Selection",
                Description = "Selects ExpressRoute as the primary connectivity solution and correctly explains that VPN Gateway traverses the public internet, violating the compliance requirement.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "networking-redundancy-model",
                Name        = "Redundancy Design",
                Description = "Identifies ExpressRoute redundancy options (dual circuits from different providers, dual peering locations) and makes a defensible recommendation given the SLA requirement.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "networking-latency-physical",
                Name        = "Latency Physical Constraints",
                Description = "Acknowledges that <10ms round-trip is a physical distance constraint (speed of light) and discusses the implication for circuit provider selection and co-location.",
                MaxPoints   = 2
            },
            new RubricCriterion
            {
                CriterionId = "networking-vpn-failover",
                Name        = "VPN Failover Compliance Analysis",
                Description = "Addresses whether VPN as failover is compliant — noting it is NOT compliant if traffic must never traverse internet, and proposes an alternative (second ExpressRoute circuit or ExpressRoute Global Reach).",
                MaxPoints   = 2
            }
        ],
        Dimensions =
        [
            new CrossCuttingDimension
            {
                Name         = "Security",
                Pitfalls     =
                [
                    "Recommending site-to-site VPN as primary or backup when the compliance requirement explicitly prohibits internet transit",
                    "Not addressing BGP route filtering between on-premises and Azure (accepting all routes creates a lateral movement risk)"
                ],
                MaxDeduction = 4
            }
        ]
    };

    // ============================================================
    // Resilience & Continuity
    // ============================================================

    // == Resilience Easy: Availability Zones vs. Multi-Region == //
    private static Scenario BuildResilienceZonesVsRegionEasyScenario() => new()
    {
        ScenarioId     = "resilience-zones-vs-region-easy-01",
        Title          = "Choosing the Right Redundancy: Zones vs. Multi-Region",
        Category       = SystemLabCategory.ResilienceAndContinuity,
        Difficulty     = Difficulty.Easy,
        EvaluationMode = EvaluationMode.SingleAnswer,
        Description    = """
            A web application has an SLA target of 99.95% uptime and must survive a datacenter
            outage within a region. There is no cross-region disaster recovery requirement —
            the business accepts that a full regional outage (affecting all three Azure zones)
            would require a longer recovery window. A colleague proposes deploying the application
            across two Azure regions (East US + West US) "for maximum safety."
            """,
        Constraints    = """
            - SLA target: 99.95% uptime.
            - No cross-region DR requirement exists.
            - The application must recover automatically from a single datacenter failure without manual intervention.
            - Budget should be minimized for the stated requirements (no gold-plating).
            """,
        RequiredTradeoffs =
        [
            "What does 99.95% uptime actually require in terms of redundancy, and does it require multi-region deployment to achieve it?",
            "What is the cost and operational complexity difference between zone-redundant deployment and a full active-passive multi-region setup for a workload with no cross-region DR requirement?"
        ],
        Rubric =
        [
            new RubricCriterion
            {
                CriterionId = "resilience-zone-redundancy",
                Name        = "Zone Redundancy Selection",
                Description = "Recommends Availability Zone-redundant deployment (zone-redundant App Service plan, ZRS storage, zone-redundant Azure SQL) rather than multi-region, and correctly maps zone redundancy to the 99.95% SLA target.",
                MaxPoints   = 4
            },
            new RubricCriterion
            {
                CriterionId = "resilience-multi-region-overkill",
                Name        = "Multi-Region Cost Analysis",
                Description = "Explains that multi-region is over-engineered for this requirement — it doubles infrastructure cost, adds latency/consistency complexity, and provides protection against a failure mode the business has not asked to protect against.",
                MaxPoints   = 2
            },
            new RubricCriterion
            {
                CriterionId = "resilience-automatic-recovery",
                Name        = "Automatic Recovery",
                Description = "Confirms that zone-redundant deployment enables automatic failover to surviving zones without manual intervention, satisfying the stated constraint.",
                MaxPoints   = 2
            }
        ],
        Dimensions =
        [
            new CrossCuttingDimension
            {
                Name         = "Cost Awareness",
                Pitfalls     =
                [
                    "Recommending active-active multi-region when the stated requirements explicitly do not include cross-region DR — doubling cost without business justification",
                    "Recommending Business Critical tier Azure SQL for zone redundancy when zone-redundant General Purpose tier satisfies the stated availability requirement"
                ],
                MaxDeduction = 3
            }
        ]
    };

    // == Resilience Medium: Tiered DR Strategy == //
    private static Scenario BuildResilienceTieredDrMedScenario() => new()
    {
        ScenarioId     = "resilience-tiered-dr-med-01",
        Title          = "Tiered Disaster Recovery for Heterogeneous Workloads",
        Category       = SystemLabCategory.ResilienceAndContinuity,
        Difficulty     = Difficulty.Medium,
        EvaluationMode = EvaluationMode.TradeoffReasoning,
        Description    = """
            An application has two components: a customer-facing payment API (critical path) and
            a batch reporting service that generates daily financial summaries. A colleague proposes
            applying the same active-passive geo-failover configuration to both components because
            "consistency in our DR approach reduces operational complexity."
            """,
        Constraints    = """
            - Payment API: RTO 15 minutes, RPO 5 minutes. Revenue impact if unavailable.
            - Batch reporting: RTO 4 hours, RPO 24 hours. Reports delayed by one day are acceptable.
            - Both components share the same Azure region (East US primary, West US secondary).
            - Budget is constrained: the finance team will approve only what is directly justified by RTO/RPO requirements.
            """,
        RequiredTradeoffs =
        [
            "What DR configuration does the payment API's 15-minute RTO / 5-minute RPO actually require, and what does it cost?",
            "Does the batch reporting service need the same configuration? What is the cost and complexity argument for using a simpler, cheaper DR posture for it?",
            "What is the hidden cost of applying one-size-fits-all DR — not just in dollars but in operational burden?"
        ],
        Rubric =
        [
            new RubricCriterion
            {
                CriterionId = "resilience-payment-dr",
                Name        = "Payment API DR Configuration",
                Description = "Selects an appropriate configuration for 15-min RTO / 5-min RPO (auto-failover group with automatic failover policy, near-synchronous geo-replication) and explains the cost.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "resilience-reporting-dr",
                Name        = "Reporting Service DR Configuration",
                Description = "Argues that batch reporting's 4-hour RTO / 24-hour RPO is satisfied by a warm standby or cold standby with geo-redundant backups — not active geo-replication — and correctly sizes down the DR posture.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "resilience-tiering-rationale",
                Name        = "Tiering Rationale",
                Description = "Explains that uniform DR wastes money on over-protecting low-criticality components AND can obscure priorities during actual failover by treating all workloads identically.",
                MaxPoints   = 2
            }
        ],
        Dimensions =
        [
            new CrossCuttingDimension
            {
                Name         = "Security",
                Pitfalls     =
                [
                    "Designing a failover that requires manually rotating secrets or connection strings without a plan for doing so within the RTO window"
                ],
                MaxDeduction = 2
            },
            new CrossCuttingDimension
            {
                Name         = "Cost Awareness",
                Pitfalls     =
                [
                    "Applying active geo-replication with automatic failover to the reporting service when its 4-hour RTO does not justify the ongoing replication cost",
                    "Not explicitly identifying what each tier costs so the finance team can evaluate the justification"
                ],
                MaxDeduction = 3
            }
        ]
    };

    // == Resilience Hard: Active-Active with Consistency Tradeoffs == //
    private static Scenario BuildResilienceActiveActiveHardScenario() => new()
    {
        ScenarioId     = "resilience-active-active-hard-01",
        Title          = "Multi-Region Active-Active: Data Consistency and Split-Brain",
        Category       = SystemLabCategory.ResilienceAndContinuity,
        Difficulty     = Difficulty.Hard,
        EvaluationMode = EvaluationMode.OpenJudgment,
        Description    = """
            A global SaaS platform wants to achieve 99.999% availability by deploying across
            three Azure regions in an active-active configuration, allowing any region to serve
            write traffic. The platform's core data model is a user profile that can be updated
            from any region. Engineers are excited about the availability gains but haven't
            discussed what happens when two regions simultaneously receive conflicting writes
            to the same user profile during a network partition.
            """,
        Constraints    = """
            - Three regions: East US, West Europe, Southeast Asia.
            - Write traffic must be accepted in any region (no single write-master).
            - Network partitions between regions must be assumed to happen (not just planned for).
            - The platform's SLA says "last write wins" for user profile updates is acceptable.
            - Latency budget: reads must be served from the nearest region (<50ms).
            """,
        RequiredTradeoffs =
        [
            "During a network partition between two regions, both accept writes to the same user profile. When the partition heals, whose write wins and how is this enforced?",
            "The platform accepts 'last write wins.' What data could be silently lost under this policy, and are there user profile fields where this is unacceptable?",
            "What does 99.999% availability actually require at the application layer beyond just deploying to multiple regions? What are the top three failure modes that still exist?",
            "Azure Cosmos DB multi-region writes are designed for this pattern. What consistency level is appropriate and what does the choice of consistency level cost you?"
        ],
        Rubric =
        [
            new RubricCriterion
            {
                CriterionId = "resilience-conflict-resolution",
                Name        = "Conflict Resolution Strategy",
                Description = "Explains last-write-wins (LWW) conflict resolution concretely — what timestamp/vector clock determines 'last' — and identifies at least one field category where LWW produces silent data loss.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "resilience-partition-behavior",
                Name        = "Partition Behavior",
                Description = "Describes what happens to writes during a partition (both regions accept independently), not just after healing — demonstrates understanding that CAP consistency is a live concern, not just a recovery concern.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "resilience-availability-gaps",
                Name        = "Remaining Availability Gaps",
                Description = "Identifies at least two failure modes beyond regional compute failure that multi-region deployment does not solve (e.g., application-layer bugs deployed simultaneously, DNS propagation delay, shared dependency failure).",
                MaxPoints   = 2
            },
            new RubricCriterion
            {
                CriterionId = "resilience-cosmos-consistency",
                Name        = "Consistency Level Selection",
                Description = "Selects an appropriate Cosmos DB consistency level (Eventual or Consistent Prefix for LWW, not Strong which disables multi-region writes) and explains the latency/consistency tradeoff.",
                MaxPoints   = 2
            }
        ],
        Dimensions =
        [
            new CrossCuttingDimension
            {
                Name         = "Security",
                Pitfalls     =
                [
                    "Not addressing how replicated data encryption keys are managed across regions during failover",
                    "Assuming 'last write wins' is safe for security-sensitive fields like email address or authentication methods without flagging the risk"
                ],
                MaxDeduction = 2
            },
            new CrossCuttingDimension
            {
                Name         = "Cost Awareness",
                Pitfalls     =
                [
                    "Not acknowledging that multi-region write RUs in Cosmos DB are significantly more expensive than single-region writes and not justifying the cost against the 99.999% target"
                ],
                MaxDeduction = 2
            }
        ]
    };

    // ============================================================
    // Monitoring & Observability
    // ============================================================

    // == Monitoring Easy: Instrumenting a New API == //
    private static Scenario BuildMonitoringNewApiEasyScenario() => new()
    {
        ScenarioId     = "monitoring-new-api-easy-01",
        Title          = "First Instrumentation Pass on a New API Deployment",
        Category       = SystemLabCategory.MonitoringAndObservability,
        Difficulty     = Difficulty.Easy,
        EvaluationMode = EvaluationMode.SingleAnswer,
        Description    = """
            Your team just deployed a new .NET 8 REST API to Azure App Service. No monitoring
            is configured yet. The on-call rotation starts in two weeks. A teammate asks:
            "What do we instrument first?" Another teammate responds: "Everything — enable
            all metrics and log every request/response body so we have full visibility."
            """,
        Constraints    = """
            - The API handles payment-adjacent data (PII in request bodies).
            - The on-call team will respond to alerts; alert volume matters.
            - Budget for log ingestion should be proportional to value.
            - The team has Application Insights available and connected.
            """,
        RequiredTradeoffs =
        [
            "What are the three most operationally valuable signals to instrument first, and why are they more important than the others?",
            "Why is 'log everything including request/response bodies' both dangerous and expensive in this context?"
        ],
        Rubric =
        [
            new RubricCriterion
            {
                CriterionId = "monitoring-golden-signals",
                Name        = "Golden Signals Priority",
                Description = "Identifies request rate, error rate, and latency (p50/p95/p99) as the first-priority signals — the golden signals — and explains why these are actionable for on-call response.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "monitoring-structured-logging",
                Name        = "Structured Logging",
                Description = "Recommends structured logging with correlation IDs (to trace a request across logs) and exception details (to diagnose failures), without logging PII in request/response bodies.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "monitoring-log-everything-rejection",
                Name        = "Reject Log-Everything",
                Description = "Explains that logging all request/response bodies creates PII exposure risk, inflates ingestion costs, and produces signal-to-noise problems that make real incidents harder to diagnose.",
                MaxPoints   = 2
            }
        ],
        Dimensions =
        [
            new CrossCuttingDimension
            {
                Name         = "Security",
                Pitfalls     =
                [
                    "Recommending logging full request/response bodies for a payment-adjacent API without acknowledging the PII exposure and compliance risk",
                    "Not mentioning that correlation IDs should not include sensitive identifiers (e.g., user IDs that could be exposed in logs accessed by support staff)"
                ],
                MaxDeduction = 3
            },
            new CrossCuttingDimension
            {
                Name         = "Cost Awareness",
                Pitfalls     =
                [
                    "Recommending enabling all available metrics and telemetry without considering Application Insights sampling or log ingestion cost at production volume"
                ],
                MaxDeduction = 2
            }
        ]
    };

    // == Monitoring Medium: Alert Noise Reduction == //
    private static Scenario BuildMonitoringAlertNoiseMedScenario() => new()
    {
        ScenarioId     = "monitoring-alert-noise-med-01",
        Title          = "Fixing an Alert System Suffering from Signal-to-Noise Failure",
        Category       = SystemLabCategory.MonitoringAndObservability,
        Difficulty     = Difficulty.Medium,
        EvaluationMode = EvaluationMode.TradeoffReasoning,
        Description    = """
            Your on-call team is receiving 30–40 alerts per day. A post-mortem analysis shows
            that roughly 80% resolve within two minutes without any human intervention, and the
            remaining 20% are real incidents. Engineers have started ignoring alerts because of
            the noise. During a real incident last month, a critical alert went unacknowledged
            for 18 minutes because the on-call engineer had normalized the alert pattern.
            """,
        Constraints    = """
            - Real incidents cannot be missed; any change must not reduce detection of true positives.
            - The alert system uses Azure Monitor with action groups tied to PagerDuty.
            - Alert evaluation window and threshold are currently configurable without code changes.
            - The team has four engineers sharing a weekly on-call rotation.
            """,
        RequiredTradeoffs =
        [
            "What is the difference between cause-based alerts (a service is unhealthy) and symptom-based alerts (users are experiencing errors), and which should drive your primary paging alerts?",
            "An alert that fires and auto-resolves in two minutes is called a 'flapping' alert. What configuration change most directly eliminates it without masking a real incident with similar behavior?",
            "How do you ensure that reducing alert volume does not reduce detection confidence for the 20% of real incidents?"
        ],
        Rubric =
        [
            new RubricCriterion
            {
                CriterionId = "monitoring-symptom-vs-cause",
                Name        = "Symptom vs. Cause Alerting",
                Description = "Correctly distinguishes symptom-based alerts (user error rate, latency SLO breach) from cause-based alerts (CPU, disk, memory), and recommends paging on symptoms while demoting cause-based alerts to warning/informational.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "monitoring-flap-suppression",
                Name        = "Flap Suppression",
                Description = "Proposes increasing the evaluation window (e.g., alert only if condition persists for 5 minutes, not 1 minute) and/or raising the threshold to filter transient spikes, and explains why this eliminates auto-resolving noise.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "monitoring-true-positive-preservation",
                Name        = "True Positive Preservation",
                Description = "Explains how to validate that the proposed changes do not suppress real incidents — e.g., backtesting thresholds against historical incident data, or running new alert rules in shadow mode before replacing existing ones.",
                MaxPoints   = 2
            }
        ],
        Dimensions =
        [
            new CrossCuttingDimension
            {
                Name         = "Cost Awareness",
                Pitfalls     =
                [
                    "Not addressing the log ingestion cost of high-frequency metric evaluation (e.g., 1-minute evaluation on 40 alert rules generates significant Log Analytics query costs)"
                ],
                MaxDeduction = 2
            }
        ]
    };

    // == Monitoring Hard: Distributed Tracing for a Microservices Failure == //
    private static Scenario BuildMonitoringDistributedTraceHardScenario() => new()
    {
        ScenarioId     = "monitoring-distributed-trace-hard-01",
        Title          = "Designing Observability to Diagnose a Distributed Payment Failure",
        Category       = SystemLabCategory.MonitoringAndObservability,
        Difficulty     = Difficulty.Hard,
        EvaluationMode = EvaluationMode.OpenJudgment,
        Description    = """
            A customer has reported intermittent payment failures over the past three days. Your
            system consists of four microservices (Gateway, Order, Payment, Notification), two
            Azure Service Bus queues, and two Azure SQL databases. The failure is intermittent —
            occurring roughly 2% of transactions — and has not been reproduced in staging.
            The on-call team spent four hours last night unable to determine the root cause
            because logs from each service are in different workspaces with no common trace ID.
            """,
        Constraints    = """
            - Each microservice currently logs independently to separate Log Analytics workspaces.
            - Service Bus messages have no correlation context attached.
            - The failure rate is 2% — low enough to be missed by broad error-rate alerts.
            - You need to diagnose the current incident AND redesign the observability architecture to prevent recurrence.
            """,
        RequiredTradeoffs =
        [
            "What is the minimum observability change needed to diagnose the current incident, and how do you do it without a full redesign deployment?",
            "What does a properly instrumented distributed system look like for this architecture? Where does the trace context originate and how does it propagate through Service Bus?",
            "A 2% failure rate won't trigger a broad error-rate alert. What alerting strategy would have caught this earlier?"
        ],
        Rubric =
        [
            new RubricCriterion
            {
                CriterionId = "monitoring-immediate-diagnosis",
                Name        = "Immediate Diagnosis Path",
                Description = "Proposes a concrete way to investigate the current incident with existing tooling — e.g., cross-workspace KQL queries, Application Insights end-to-end transaction search, or correlating by customer/order ID as a substitute trace ID.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "monitoring-trace-propagation",
                Name        = "Distributed Trace Architecture",
                Description = "Describes W3C traceparent header propagation from Gateway through services, and explains how to carry trace context through Service Bus message properties so async hops are traceable.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "monitoring-low-rate-alerting",
                Name        = "Low-Rate Failure Alerting",
                Description = "Proposes an alert strategy for low-rate failures — e.g., alert on payment-specific error rate (not overall error rate), percentile-based alert on payment latency as a proxy, or anomaly detection on payment success rate.",
                MaxPoints   = 2
            },
            new RubricCriterion
            {
                CriterionId = "monitoring-unified-observability",
                Name        = "Unified Observability Design",
                Description = "Recommends centralizing logs into a single Log Analytics workspace (or using cross-workspace queries with Azure Monitor), and explains the operational benefit of a single query surface.",
                MaxPoints   = 2
            }
        ],
        Dimensions =
        [
            new CrossCuttingDimension
            {
                Name         = "Security",
                Pitfalls     =
                [
                    "Recommending that correlation IDs or trace headers include customer PII (e.g., email address or full account number) that would be stored in logs",
                    "Recommending a unified workspace without addressing workspace access controls — not all engineers should see all logs"
                ],
                MaxDeduction = 3
            }
        ]
    };

    // ============================================================
    // Automation & IaC
    // ============================================================

    // == Automation Easy: Script vs. IaC == //
    private static Scenario BuildAutomationScriptVsIacEasyScenario() => new()
    {
        ScenarioId     = "automation-script-vs-iac-easy-01",
        Title          = "Shell Script vs. Declarative IaC for Infrastructure Deployment",
        Category       = SystemLabCategory.AutomationAndIaC,
        Difficulty     = Difficulty.Easy,
        EvaluationMode = EvaluationMode.SingleAnswer,
        Description    = """
            A colleague maintains Azure infrastructure using a shell script that calls Azure CLI
            commands in sequence: it creates a storage account, configures CORS settings,
            assigns a role, and sets up a diagnostic log sink. The script runs manually by the
            senior engineer before each environment deployment. When a junior engineer ran
            the script twice by mistake, it partially failed on the second run with "resource
            already exists" errors, leaving the environment in an inconsistent state.
            """,
        Constraints    = """
            - The infrastructure must be deployable consistently across dev, staging, and prod environments.
            - Deployments must be repeatable without manual cleanup between runs.
            - Any engineer on the team should be able to trigger a deployment safely.
            """,
        RequiredTradeoffs =
        [
            "What property does the shell script lack that caused the partial failure on the second run, and what does that word mean in the context of infrastructure deployment?",
            "How does declarative IaC (Bicep or Terraform) handle the 'resource already exists' case differently from a script, and why does that change the operational risk?"
        ],
        Rubric =
        [
            new RubricCriterion
            {
                CriterionId = "automation-idempotency",
                Name        = "Idempotency",
                Description = "Correctly defines idempotency (same result on every run regardless of how many times executed) and explains that the script fails this property because CLI create commands error on existing resources.",
                MaxPoints   = 4
            },
            new RubricCriterion
            {
                CriterionId = "automation-declarative-model",
                Name        = "Declarative IaC Model",
                Description = "Explains that Bicep or Terraform declare desired state and the platform reconciles current state to desired — create if missing, update if different, skip if already correct — making reruns safe by design.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "automation-drift-risk",
                Name        = "Drift Risk",
                Description = "Notes that a script has no state model — it cannot detect or correct config drift between runs — while IaC with a state file or ARM deployment can detect and reconcile drift.",
                MaxPoints   = 1
            }
        ],
        Dimensions =
        [
            new CrossCuttingDimension
            {
                Name         = "Security",
                Pitfalls     =
                [
                    "Recommending the script be fixed with --if-not-exists flags rather than migrating to IaC, which preserves the stateless, drift-blind nature of the script approach",
                    "Not mentioning that scripts run manually by a single senior engineer create a knowledge silo and deployment bottleneck"
                ],
                MaxDeduction = 2
            }
        ]
    };

    // == Automation Medium: Detecting and Reconciling Config Drift == //
    private static Scenario BuildAutomationDriftReconcileMedScenario() => new()
    {
        ScenarioId     = "automation-drift-reconcile-med-01",
        Title          = "Detecting and Reconciling Configuration Drift After Manual Changes",
        Category       = SystemLabCategory.AutomationAndIaC,
        Difficulty     = Difficulty.Medium,
        EvaluationMode = EvaluationMode.TradeoffReasoning,
        Description    = """
            Your team deployed production infrastructure using Bicep six months ago. Since then,
            engineers have made manual changes in the Azure Portal: added a firewall rule,
            changed a storage SKU from Standard to Premium, and modified a role assignment.
            None of these changes are reflected in the Bicep templates. The infrastructure
            is now out of sync with the code, and you need to reconcile the drift without
            causing an outage or losing the intentional manual changes.
            """,
        Constraints    = """
            - Some manual changes may be intentional (the firewall rule was added for a legitimate reason).
            - The Bicep deployment is configured in Incremental mode (does not delete resources not in the template).
            - You cannot afford an outage to reconcile drift.
            - The team must decide: does the template become the source of truth, or do the manual changes?
            """,
        RequiredTradeoffs =
        [
            "What is the difference between Incremental and Complete mode in Bicep, and why does Complete mode create risk in a drift-reconciliation scenario?",
            "Before touching anything, how do you determine the scope and nature of the drift? What tooling gives you a diff between current Azure state and desired Bicep state?",
            "How do you reconcile drift safely when some manual changes might be intentional? What is the process for deciding what to keep vs. override?"
        ],
        Rubric =
        [
            new RubricCriterion
            {
                CriterionId = "automation-what-if",
                Name        = "Drift Detection via What-If",
                Description = "Proposes `az deployment group what-if` (or equivalent Terraform plan) to produce a diff of current state vs. desired state before making any changes — critical for safe drift reconciliation.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "automation-complete-mode-risk",
                Name        = "Complete Mode Risk",
                Description = "Explains that Complete mode deletes resources present in Azure but absent from the template — a dangerous choice when manual changes may be intentional — and recommends Incremental mode with explicit property updates.",
                MaxPoints   = 2
            },
            new RubricCriterion
            {
                CriterionId = "automation-change-review",
                Name        = "Manual Change Review Process",
                Description = "Describes a process for reviewing each manual change (firewall rule, SKU change, role assignment) against its intent before deciding to codify or revert it, involving the person who made the change.",
                MaxPoints   = 2
            },
            new RubricCriterion
            {
                CriterionId = "automation-prevent-recurrence",
                Name        = "Prevent Recurrence",
                Description = "Proposes a mechanism to prevent future manual drift — e.g., Azure Policy denying changes outside IaC pipelines, branch protection on the Bicep repo, or required PR review for template changes.",
                MaxPoints   = 1
            }
        ],
        Dimensions =
        [
            new CrossCuttingDimension
            {
                Name         = "Security",
                Pitfalls     =
                [
                    "Recommending Complete mode deployment without first reviewing all resources in Azure that might be deleted",
                    "Not validating that manually-added role assignments are appropriate before codifying them into the template"
                ],
                MaxDeduction = 3
            }
        ]
    };

    // == Automation Hard: Multi-Environment Promotion Pipeline == //
    private static Scenario BuildAutomationPipelinePromotionHardScenario() => new()
    {
        ScenarioId     = "automation-pipeline-promotion-hard-01",
        Title          = "Designing a Safe Multi-Environment Promotion Pipeline",
        Category       = SystemLabCategory.AutomationAndIaC,
        Difficulty     = Difficulty.Hard,
        EvaluationMode = EvaluationMode.OpenJudgment,
        Description    = """
            A team currently deploys infrastructure and application code as follows: dev is
            deployed manually via Bicep CLI by whoever is working on the feature; staging is
            "synced from dev when it seems stable"; prod is deployed by a senior engineer
            "when it's ready" with no formal process. This approach has caused two prod
            incidents: a misconfiguration deployed to prod that was never in staging, and
            a secrets rotation in staging that was not replicated to prod before a release.
            """,
        Constraints    = """
            - Infrastructure (Bicep) and application code must be promoted through the same pipeline.
            - Dev is the experimental environment; staging must be a production mirror.
            - Secrets (connection strings, API keys) are managed in Azure Key Vault per environment.
            - The team has Azure DevOps available and uses it for code reviews, but not for deployments.
            - A hotfix path must exist that allows emergency prod deployments in under 30 minutes.
            """,
        RequiredTradeoffs =
        [
            "What does 'staging must be a production mirror' require from the pipeline design, and what specific changes prevent the incident where a misconfiguration went to prod without being in staging?",
            "How should secrets be managed across environments in the pipeline? What is wrong with copying secrets from staging to prod manually, and what is the correct pattern?",
            "A hotfix needs to go to prod in under 30 minutes. How do you design a fast-track path that doesn't bypass safety gates entirely?",
            "What is the minimum gate between staging and prod that provides meaningful confidence without blocking rapid releases?"
        ],
        Rubric =
        [
            new RubricCriterion
            {
                CriterionId = "automation-environment-parity",
                Name        = "Environment Parity",
                Description = "Defines environment parity (staging uses the same Bicep templates, same SKUs, same configuration structure as prod) and proposes a single parameterized template with environment-specific parameter files — not a separate template per environment.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "automation-secrets-pipeline",
                Name        = "Secrets Management in Pipeline",
                Description = "Proposes Key Vault references in pipeline variable groups (not hardcoded secrets), environment-specific Key Vaults with the same secret names, and explains why manual secret copying is error-prone and creates a sync problem.",
                MaxPoints   = 3
            },
            new RubricCriterion
            {
                CriterionId = "automation-hotfix-path",
                Name        = "Hotfix Path Design",
                Description = "Designs a hotfix path that skips the full staging soak time but preserves a minimum gate (e.g., a required approval from a second engineer, automated smoke tests against prod), and explains what risk is accepted vs. what is preserved.",
                MaxPoints   = 2
            },
            new RubricCriterion
            {
                CriterionId = "automation-staging-gate",
                Name        = "Staging-to-Prod Gate",
                Description = "Proposes a concrete gate that provides real confidence: automated smoke tests or integration tests run against staging, a minimum soak period, or a required approval from someone other than the deploying engineer.",
                MaxPoints   = 2
            }
        ],
        Dimensions =
        [
            new CrossCuttingDimension
            {
                Name         = "Security",
                Pitfalls     =
                [
                    "Storing secrets as plaintext in pipeline variables or YAML files instead of Key Vault references",
                    "Designing a hotfix path with no approval gate — a single engineer can deploy to prod with no second set of eyes",
                    "Using the same service principal with prod-write permissions for dev and staging deployments"
                ],
                MaxDeduction = 4
            }
        ]
    };
}
