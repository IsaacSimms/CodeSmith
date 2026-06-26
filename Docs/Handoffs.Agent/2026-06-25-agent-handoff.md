# Thread Handoff — CodeSmith (Agent)

**Date:** 2026-06-25  
**Handoff Mode:** Implementation-to-Implementation or Ideation-to-Implementation  
**Primary Goal for Receiving Agent:** Continue work on CodeSmith with full context of the current architecture, protection seam, and development practices.

---

## 1. Project Overview & Current State

**CodeSmith** is a .NET 8 Clean Architecture learning + potential SaaS product focused on technical education (paired programming, prompt engineering, infrastructure judgment).

**Current Major Components:**
- **Backend**: `CodeSmith.Api` (ASP.NET Core), `CodeSmith.Core`, `CodeSmith.Infrastructure`
- **Protection Seam** (recently completed and verified): `UsageEnforcer` + three `UsageEnforcing*` decorators around LLM services. Enforces per-`objectId` free token quotas + records usage/cost.
- **Database**: Azure SQL Serverless (`db-codesmith-prod-centralus-001`) in `rg-codesmith-prod-centralus-001`.
- **Hosting**: Azure Container Apps (`ca-codesmith-api-001`) + ACR + Managed Identity.
- **Auth (Dev)**: `X-Debug-User-Id` header + `AllowedDebugObjectIds` allow-list. Full Entra External ID is planned but not yet implemented.
- **Deploy**: GitHub Actions workflow (`deploy-azure.yml`) using Service Principal + Managed Identity for ACR pull.

---

## 2. Architecture & Ubiquitous Language (Important for Agents)

CodeSmith follows strict **Clean Architecture** + the project's custom Ubiquitous Language:

- **Module**: Anything with an interface and implementation (function, class, package, or cross-cutting slice).
- **Seam**: A place where behavior can be altered without editing that place. The protection seam (decorators around LLM services) is a canonical example.
- **Interface vs Implementation**: Be precise. Prefer deep modules with high leverage and locality.
- **Adapter**: Concrete thing that satisfies an interface at a seam.

**Key Current Seams:**
- `IUsageEnforcer` + decorators (non-negotiable — all LLM calls must go through them).
- `ICurrentUser` (single source of truth for `objectId`).
- `ILlmPricing` (testable pricing table in Core).

**Never** call raw LLM services (`AnthropicLlmService`, etc.) directly from controllers or orchestrators.

---

## 3. Important Decisions & Current Direction

| Area | Decision / Direction | Status | Notes for Future Work |
|------|----------------------|--------|-----------------------|
| **Cost Protection** | Hardened seam with 20k token / 48h window + dual IP caps + free-first logic | **Verified working** | Do not bypass or modify `IUsageEnforcer` |
| **Authentication (Dev)** | `X-Debug-User-Id` + allow-list | Current supported path | Full Entra External ID is future work |
| **Billing** | Separate module that only credits `PaidCreditsBalance` | Next locked increment | Must not touch usage enforcement logic |
| **Database** | Azure SQL Serverless + EF Core + Managed Identity | Stable | Connection string uses Entra auth locally |
| **Deploy** | GitHub Actions → ACR → Container Apps with MI | Stable | Temporary debug steps should be removed when found |
| **Resource Grouping** | `rg-codesmith-prod-centralus-001` is the canonical RG | Recently cleaned up | SQL Server was moved here in this thread |

**Next Locked Increment (per prior handoffs):**  
**Stripe Prepaid Credits Module** — Create a separate billing module for purchasing credit packs. Webhook should only credit `PaidCreditsBalance`.

---

## 4. What an Agent Must Know

- **Protection seam is non-negotiable**. Any new LLM feature must go through the decorators.
- `HttpCurrentUser` + debug header is how we currently identify users in Development. Do not bypass it.
- When adding new features that spend tokens/credits, they must be protected.
- Database migrations are applied manually (`dotnet ef database update`). Never auto-migrate in production.
- Clean Architecture + project conventions (block `// == Title == //` comments, no member XML docs on most members, edit-in-place where possible) must be followed.
- Testing: Manual smoke testing via Thunder Client + Azure Portal Query Editor is currently the primary method. Unit tests exist for critical components (pricing, enforcer, new debug handler).

---

## 5. Relevant Artifacts & Context

- Protection seam implementation: See June 18 usage enforcement handoff and buildout recap.
- First production deploy: June 21 deploy + handoff documents.
- This thread (June 25): Added `DebugAuthenticationHandler`, moved SQL Server to correct RG, resumed database, and performed first successful end-to-end smoke test of the seam.

**Paste-ready summary for new agent:**

"Picking up CodeSmith after protection seam verification. The `UsageEnforcing*` decorators + `IUsageEnforcer` are now confirmed working in production. Debug auth is functional via `X-Debug-User-Id`. SQL Server has been moved to the correct resource group and the database is online.

Next priority per locked decisions: Stripe prepaid credits module (separate billing concern that only credits `PaidCreditsBalance`).

Full architecture, seams, and non-negotiables are documented in the agent handoff. Do not bypass the protection seam."

---

**End of Agent Handoff**