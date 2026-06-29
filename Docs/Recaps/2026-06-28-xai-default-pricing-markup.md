# Make xAI the Default Provider + Correct Pricing & Add Markup

**Date:** 2026-06-28
**Type:** planning + implementation
**Environment / Systems:** CodeSmith (.NET 8 API/Infrastructure/Core, React 19 + Vitest frontend), EF Core + SQL Server usage seam

## TL;DR
Flipped CodeSmith's default AI provider from Anthropic to xAI (backend `Ai:ActiveProvider` now authoritative; frontend honors it for first-time users while preserving the provider toggle), corrected three wrong rows in the `LlmPricing` rate table, and added a config-driven 2.0× paid-credit markup with the usage ledger now recording both charged price and raw provider cost. Frontend is fully implemented and green (112 tests); the backend code + tests are written but the build/test run and the EF migration are blocked by a running `CodeSmith.Api` dev server holding the DLLs.

## Context & Goal
The ask was to change the default model from Anthropic to xAI. Codebase review showed the provider abstraction was already mature (`AiProvider` enum incl. `Xai`, `OpenAiCompatibleLlmService` driving xAI's OpenAI-compatible endpoint, `LlmServiceFactory` keyed by provider, per-session provider selection). So this was a configuration + default-selection change, not new architecture. A `/grill-me` session widened scope to pricing correctness and margin once `LlmPricing.cs` was found to be the cost engine for the paid-credit seam.

## Key Points Explored
- **The real "default" seam.** The only thing that actually picks a user's default provider was the frontend `useProviderPreference` hook, hardcoded to `"Anthropic"`. Backend `Ai:ActiveProvider` was cosmetic — only echoed by `GET /api/providers`, ignored by the frontend.
- **Model name.** `grok-4.3` is real and current (verified via xAI docs) — the flagship recommended for chat+coding; xAI retired the cheap/fast variants, so one model serves both accurate and fast tiers.
- **`LlmPricing` is the cost engine**, not a display table. `ComputeCostUsd` is debited from `PaidCreditsBalance` and written to the ledger; `EstimateUpperBoundCost` is the pre-call affordability gate. Audit found 3 of 5 rate rows wrong.
- **Free quota is token-based**, unaffected by pricing/markup — markup only changes dollar burn on the paid path, which needs a Stripe/top-up path that doesn't exist yet. So markup ships as correct, dormant plumbing.
- **Ledger is append-only**, and the rate basis isn't stored — so raw cost must be captured at write time or historical margin is unrecoverable. Hence a second column rather than price-only.

## Decisions & Outcomes

**Corrected raw-cost rate table (per 1k tokens):**

| Provider / model | Was | Now |
|---|---|---|
| `claude-sonnet-4-6` | 0.003 / 0.015 | unchanged (correct) |
| `claude-haiku-4-5-20251001` | 0.0008 / 0.004 | **0.001 / 0.005** |
| `gpt-4.1` | 0.002 / 0.008 | unchanged (correct) |
| `gpt-4.1-mini` | 0.00015 / 0.0006 | **0.0004 / 0.0016** |
| `grok-4.3` | 0.002 / 0.010 | **0.00125 / 0.0025** |

**Default flip (done):**
- `appsettings.json` `Ai.ActiveProvider` → `"Xai"`; `AiOptions` default → `"Xai"`.
- `useProviderPreference(serverDefault?)` — stored localStorage choice always wins; first-time users follow the server's `activeProvider` (passed in from `HomePage`); never persists the server default, so the default can move later. Provider toggle is fully preserved.

**Pricing + markup (code done):**
- `LlmPricing` made config-aware (`IOptions<UsageOptions>`); rate table holds true provider cost; new `ComputeChargeUsd` = raw × markup; `EstimateUpperBoundCost` now multiplies by markup so the gate reserves against the charge.
- `UsageOptions.PaidMarkupMultiplier` (default `2.0m`), surfaced in `appsettings.json` under `Usage`.
- `UsageLedgerEntry.ProviderCostUsd` (nullable) added; DbContext given matching precision (18,6). `CostUsd` now means the charged amount.
- `IUsageEnforcer.RecordActualAsync` takes both `chargeUsd` + `providerCostUsd`; debits the charge, records both. Decorator computes both from `LlmPricing`.

**Tests:**
- Frontend: new `useProviderPreference.test.tsx` (Map-backed localStorage stub to work around the partial polyfill in the test env); full suite **112 passed**.
- Backend (written, not yet run): `LlmPricingTests` (corrected rates, markup, charge), `UsageEnforcerTests` (ledger records both charge + provider cost; balance debits charge), `SessionControllerTests` (`GetProviders` reports `"Xai"`).

**Verified:** C# compiled cleanly — the only build failures were MSB3021/MSB3026/MSB3027 file-copy locks from the running API, not `CSxxxx` errors.

## Open Questions / Next Steps
1. **Stop the running `CodeSmith.Api` (PID 26276)** so the DLLs unlock.
2. `dotnet build` to confirm backend compilation.
3. `dotnet ef migrations add AddProviderCostToUsageLedger` (additive nullable column, no backfill) + regenerate snapshot; apply to dev DB.
4. `dotnet test` — confirm pricing/markup/ledger/provider assertions green.
5. Markup is inert until a paid-credit/Stripe path exists; revisit when payments are wired.
6. Set a valid `Xai:ApiKey` (local `appsettings.Development.json` / Azure Key Vault) for a live grok-4.3 smoke test.

## Artifacts
- **Plan:** `C:\Users\primi\.claude\plans\i-have-the-xai-radiant-ritchie.md` (approved).
- **Backend changed:** `CodeSmith.Core/Interfaces/ILlmPricing.cs`, `CodeSmith.Core/Interfaces/IUsageEnforcer.cs`, `CodeSmith.Core/Models/Usage/UsageLedgerEntry.cs`, `CodeSmith.Infrastructure/Services/Usage/LlmPricing.cs`, `CodeSmith.Infrastructure/Services/Usage/UsageEnforcer.cs`, `CodeSmith.Infrastructure/Services/Usage/Decorators/UsageEnforcingLlmService.cs`, `CodeSmith.Infrastructure/Configuration/UsageOptions.cs`, `CodeSmith.Infrastructure/Configuration/AiOptions.cs`, `CodeSmith.Infrastructure/Persistence/CodeSmithDbContext.cs`, `CodeSmith.Api/appsettings.json`.
- **Backend tests:** `CodeSmith.Tests/Infrastructure/Usage/LlmPricingTests.cs`, `CodeSmith.Tests/Infrastructure/Usage/UsageEnforcerTests.cs`, `CodeSmith.Tests/Api/SessionControllerTests.cs`.
- **Frontend changed:** `CodeSmith.Web/src/hooks/useProviderPreference.ts`, `CodeSmith.Web/src/features/home/components/HomePage.tsx`, new `CodeSmith.Web/src/hooks/useProviderPreference.test.tsx`.
- **Not yet created:** EF migration `AddProviderCostToUsageLedger`.
