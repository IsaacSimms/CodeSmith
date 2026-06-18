# SaaS Data Layer and Usage Enforcement Implementation

**Date:** 2026-06-18
**Type:** implementation
**Environment / Systems:** .NET 8 / ASP.NET Core, Azure SQL (pre-provisioned with Managed Identity), Windows

## TL;DR

Implemented the complete data layer (EF Core + Azure SQL entities) and usage enforcement seam (decorators + IUsageEnforcer) so every LLM call is protected by per-`objectId` free monthly token quota checks before spend and actual usage/cost is recorded after. All LLM paths (tutoring, PromptLab, SystemLab) are covered. Build and full test suite green. Followed the locked SaaS decisions and grilled plan exactly.

## Context & Goal

The project is moving from dev-only to public SaaS. Key risk: owner pays for all LLM usage. The thread's goal was to deliver the minimum viable cost-protection seams: persistent per-user balances/ledger, pre-call enforcement with strong consistency, and post-call actual recording. This enables safe free-tier exploration and sets up future prepaid credits via Stripe without changing the enforcement logic.

Source material: the three decision/recap/handoff docs provided at the start of the thread (locked choices on Entra objectId, prepaid model first, free quota, Azure SQL + EF, usage seam separate from billing, decorators recommended, etc.).

## Key Points Explored

- Current architecture: LLM access only through `ILlmServiceFactory` + keyed `ITutoringLlmService` / `IPromptLabLlmService` / `ISystemLabLlmService` implementations. `LlmResponse` only tracked input tokens.
- Enforcement must be a true seam in front of the LLM adapter (per Clean Architecture + UL).
- Free quota is token-based hard stop; paid credits are cost-based.
- Need for `ICurrentUser` seam to get stable `objectId`.
- Pre-check must use upper-bound estimate (input + maxTokens) because output size unknown.
- Strong consistency required on balance to prevent concurrent overspend.
- Verification limited to `dotnet build` + `dotnet test` (explicit user direction — no live server/HTTP exercises in this phase).
- Grilling resolved: all paths, hybrid auth (dev header + Entra skeleton), free-first, decorators + lean highest-rate estimate, simple string for feature, migrations as later job.

## Decisions & Outcomes

- Extended `LlmResponse` with `OutputTokensUsed` and `Model`; populated in all three adapters.
- New Core entities (`CreditBalance`, `UsageLedgerEntry`), repo interfaces, `IUsageEnforcer`, `ICurrentUser`, `ILlmPricing`.
- `ILlmPricing` with static testable rate table + conservative upper-bound estimator.
- `UsageEnforcer` impl with monthly reset, free-first debit, estimate check, actual record.
- Three decorators registered in place of raw keyed LLM services (transparent protection, orchestrators untouched).
- EF `CodeSmithDbContext`, repositories, indexes, RowVersion.
- `UsageOptions` for configurable free quota.
- `HttpCurrentUser` (dev `X-Debug-User-Id` bypass + Entra claims).
- 402 mapper for `InsufficientQuotaException`.
- `[Authorize]` only on actual LLM-spending actions across the three controllers.
- Minimal auth pipeline skeleton.
- All changes respect project conventions (block comments, no member `///`, Clean Arch, edit existing first).
- Result: full build + 198 tests passing. Seams complete for the stated goals.

## Open Questions / Next Steps

- Stripe prepaid credit packs + secure webhook that credits `PaidCreditsBalance` (separate billing module).
- Full Entra External ID configuration and frontend token flow.
- Apply initial migration against real Azure SQL (later job, not auto-Migrate).
- Per-objectId rate limiting.
- Any owner-visible usage reporting.

## Artifacts

- `Handoffs/2026-06-18-usage-enforcement-handoff.md` (forward-looking)
- `Recaps/2026-06-18-usage-enforcement-buildout.md` (this file)
- New/changed files listed in the handoff document (Core Models/Usage/*, Interfaces/*, Infrastructure/Services/Usage/* + Persistence/*, Api additions, DI wiring, controller Authorize attributes, LlmResponse, etc.).
- Session plan used: the detailed grilled plan.md produced during the thread.
- Original locked decisions in the user's provided Downloads files.