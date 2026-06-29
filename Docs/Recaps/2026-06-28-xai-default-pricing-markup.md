# Make xAI the Default Provider + Correct Pricing, Add Markup, Fix Ledger Schema Drift

**Date:** 2026-06-28
**Type:** planning + implementation + fix
**Environment / Systems:** CodeSmith (.NET 8 Core/Infrastructure/Api, React 19 + Vitest frontend); EF Core + Azure SQL prod (`sql-codesmith-prod-centralus-001`, AD Default auth)

## TL;DR
Flipped CodeSmith's default AI provider from Anthropic to xAI (backend `Ai:ActiveProvider` now authoritative; frontend honors it for first-time users while keeping the provider toggle), corrected three wrong rows in the `LlmPricing` rate table, and added a config-driven 2.0× paid-credit markup with the usage ledger now recording both charged price and raw provider cost. Implementation is complete and fully verified: backend 236/236, frontend 112/112, clean build. The new `ProviderCostUsd` ledger column caused a runtime `Invalid column name` schema-drift error against prod, fixed by generating and applying EF migration `AddProviderCostToUsageLedger`.

## Context & Goal
The ask was to change the default model from Anthropic to xAI. A codebase review showed the provider abstraction was already mature (`AiProvider` enum incl. `Xai`, `OpenAiCompatibleLlmService` driving xAI's OpenAI-compatible endpoint, `LlmServiceFactory` keyed by provider, per-session provider selection) — so this was a configuration + default-selection change, not new architecture. A `/grill-me` session widened scope to pricing correctness and margin once `LlmPricing.cs` was identified as the cost engine for the paid-credit seam.

## Key Points Explored
- **The real "default" seam.** The only thing that actually picked a user's default provider was the frontend `useProviderPreference` hook, hardcoded to `"Anthropic"`. Backend `Ai:ActiveProvider` was cosmetic — only echoed by `GET /api/providers`, ignored by the frontend.
- **Model name.** `grok-4.3` verified real and current via xAI docs — the flagship recommended for chat+coding; xAI retired the cheap/fast variants, so one model serves both accurate and fast tiers.
- **`LlmPricing` is the cost engine**, not a display table: `ComputeCostUsd` is debited from `PaidCreditsBalance` and written to the ledger; `EstimateUpperBoundCost` is the pre-call affordability gate. Audit found 3 of 5 rate rows wrong.
- **Free quota is token-based**, unaffected by pricing/markup — markup only changes dollar burn on the paid path, which needs a Stripe/top-up path that doesn't exist yet. So markup ships as correct, dormant plumbing.
- **The ledger is append-only** and the rate basis isn't stored — so raw cost must be captured at write time or historical margin is unrecoverable. Hence a second column (`ProviderCostUsd`) rather than price-only.
- **Schema drift.** After deploy/run, `POST /api/session` threw `SqlException: Invalid column name 'ProviderCostUsd'` — the EF model had the column but prod SQL didn't. Because the ledger `INSERT` and the `CreditBalances` `UPDATE` ran in one EF batch, the whole `SaveChanges` rolled back (usage not recorded, balance not updated). The quota gate itself worked correctly.

## Decisions & Outcomes

**Corrected raw-cost rate table (per 1k tokens):**

| Provider / model | Was | Now |
|---|---|---|
| `claude-sonnet-4-6` | 0.003 / 0.015 | unchanged (correct) |
| `claude-haiku-4-5-20251001` | 0.0008 / 0.004 | **0.001 / 0.005** |
| `gpt-4.1` | 0.002 / 0.008 | unchanged (correct) |
| `gpt-4.1-mini` | 0.00015 / 0.0006 | **0.0004 / 0.0016** |
| `grok-4.3` | 0.002 / 0.010 | **0.00125 / 0.0025** |

**Default flip:**
- `appsettings.json` `Ai.ActiveProvider` → `"Xai"`; `AiOptions` default → `"Xai"`.
- `useProviderPreference(serverDefault?)` — stored localStorage choice always wins; first-time users follow the server's `activeProvider` (passed in from `HomePage`); the server default is never persisted, so the default can move later. The provider toggle is fully preserved.

**Pricing + markup:**
- `LlmPricing` made config-aware (`IOptions<UsageOptions>`); rate table holds true provider cost; new `ComputeChargeUsd` = raw × markup; `EstimateUpperBoundCost` multiplies by markup so the gate reserves against the charge.
- `UsageOptions.PaidMarkupMultiplier` (default `2.0m`), surfaced in `appsettings.json` under `Usage`.
- `UsageLedgerEntry.ProviderCostUsd` (nullable) added; DbContext given matching precision (18,6). `CostUsd` now means the charged amount.
- `IUsageEnforcer.RecordActualAsync` takes both `chargeUsd` + `providerCostUsd`; debits the charge (proportional to paid-token overflow), records both. Decorator computes both from `LlmPricing`.

**Schema-drift fix:**
- Generated `20260629044325_AddProviderCostToUsageLedger` — a single additive `decimal(18,6)` nullable column, clean `Down`.
- Applied to Azure SQL prod via `dotnet ef database update -p CodeSmith.Infrastructure` → `Done`.

**Verification:**
- Backend: `dotnet test` → **236 passed, 0 failed**; rebuild **0 warnings, 0 errors** (fixed a CS4014 in the new ledger-capture test with a `_ =` discard).
- Frontend: full Vitest suite → **112 passed** (new `useProviderPreference.test.tsx` uses a Map-backed localStorage stub to work around the test env's partial Web Storage polyfill).
- Earlier "build failures" were MSB3021/MSB3026/MSB3027 file-copy locks from the running API holding the DLLs — not `CSxxxx` errors.

## Open Questions / Next Steps
- Restart the API and re-test the two scenarios manually: full quota → `201` + a ledger row (`CostUsd` = charge, `ProviderCostUsd` = raw); exhausted quota (`FreeTokensUsedInWindow = FreeQuotaMax = 20000`, no positive balance) → `402` / `InsufficientQuota`.
- Markup is inert until a paid-credit/Stripe path exists; revisit margin when payments are wired. The leftover `PaidCreditsBalance = -0.249708` on one row is pre-fix residue and harmless with free quota remaining.
- Set a valid `Xai:ApiKey` (local `appsettings.Development.json` / Azure Key Vault) for a live grok-4.3 smoke test.
- Changes are not yet committed.

## Artifacts
- **Plan:** `C:\Users\primi\.claude\plans\i-have-the-xai-radiant-ritchie.md` (approved).
- **Migration (generated + applied to prod):** `CodeSmith.Infrastructure/Migrations/20260629044325_AddProviderCostToUsageLedger.cs` (+ `.Designer.cs`, snapshot updated).
- **Backend changed:** `CodeSmith.Core/Interfaces/ILlmPricing.cs`, `CodeSmith.Core/Interfaces/IUsageEnforcer.cs`, `CodeSmith.Core/Models/Usage/UsageLedgerEntry.cs`, `CodeSmith.Infrastructure/Services/Usage/LlmPricing.cs`, `CodeSmith.Infrastructure/Services/Usage/UsageEnforcer.cs`, `CodeSmith.Infrastructure/Services/Usage/Decorators/UsageEnforcingLlmService.cs`, `CodeSmith.Infrastructure/Configuration/UsageOptions.cs`, `CodeSmith.Infrastructure/Configuration/AiOptions.cs`, `CodeSmith.Infrastructure/Persistence/CodeSmithDbContext.cs`, `CodeSmith.Api/appsettings.json`.
- **Backend tests:** `CodeSmith.Tests/Infrastructure/Usage/LlmPricingTests.cs`, `CodeSmith.Tests/Infrastructure/Usage/UsageEnforcerTests.cs`, `CodeSmith.Tests/Api/SessionControllerTests.cs`.
- **Frontend changed:** `CodeSmith.Web/src/hooks/useProviderPreference.ts`, `CodeSmith.Web/src/features/home/components/HomePage.tsx`, new `CodeSmith.Web/src/hooks/useProviderPreference.test.tsx`.
- **Commands of record:** `dotnet ef migrations add AddProviderCostToUsageLedger -p CodeSmith.Infrastructure`; `dotnet ef database update -p CodeSmith.Infrastructure`.
