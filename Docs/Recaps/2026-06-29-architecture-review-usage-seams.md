# Architecture Review: Usage Seam, Session Locking, Pricing Validation

**Date:** 2026-06-29
**Type:** fix
**Environment / Systems:** CodeSmith (.NET 8 backend — Core/Infrastructure/Api); xUnit + NSubstitute tests

## TL;DR
Ran `/improve-codebase-architecture`, found the cost/credit docs had drifted from the code and three real architecture risks. Reconciled `context.md` + `CLAUDE.md`, then fixed all three in sequence (TDD, each red→green-proven): made the usage seam actually *reserve* (closing an N× overspend), added a unified per-session lock (closing history-corruption 400s), and bound configured model names to the pricing table with fail-fast startup validation. Suite went 241 → 257 tests, all green.

## Context & Goal
User asked for an architecture pass prioritizing things that **break functionality, cause unnecessary spend, or are big problems**. Early on the user pointed out `context.md` and `CLAUDE.md` existed (an initial Glob miss had hidden `context.md`); reading it revealed the doc described a billing model the code no longer used. Goal became: get docs to ground truth, then fix the highest-priority architecture issues one at a time (plan/grill → implement → next).

## Key Points Explored
- **Doc drift (cost subsystem).** `context.md` (dated 2026-06-19) predated 5 commits of usage rework. It described a *monthly free-quota reset* and wrong `CreditBalance`/`UsageLedgerEntry` field lists; the code actually used a **48h window** from `FirstSeenUtc`, a **per-IP 60k free cap** (`IpFreeUsage`), a **markup** (`PaidMarkupMultiplier`) with `CostUsd` vs `ProviderCostUsd`, **xAI as default provider**, and **Entra auth + forwarded headers** already wired (doc said "planned").
- **#1 Reservation gap (spend).** `IUsageEnforcer.CheckAndReserveAsync` was named reserve but only *checked* — balance moved later in `RecordActualAsync`, after the LLM call. One Prompt Lab submit fans out to **up to 2N parallel completions** (`PromptSimulator` + `PromptEvaluator`, both `Task.WhenAll`); all read the same balance → **N× overspend**. `CreditBalance.RowVersion` existed but unused.
- **#2 Session locking (breakage).** Only System Lab serialized session mutation (bespoke `GetLock`); Tutoring + Prompt Lab raced `List<ChatMessage>` on concurrent turns → corrupted user/assistant alternation → provider **400**.
- **#3 Pricing drift (mis-charge).** `LlmPricing.RateTable` keys and the configured `AccurateModel`/`FastModel` were unlinked; a drift silently fell back to the highest rate for both directions.

## Decisions & Outcomes
- **Docs reconciled.** Full sweep of `context.md` (48h window, IP cap, markup, `ProviderCostUsd`, middleware/auth, seam table, model lists); lean fixes to `CLAUDE.md` (multi-provider, real request shapes, 402/auth, pointer to `context.md`). Updated again after each fix.
- **#1 — Reserve→Settle→Release lifecycle.** Reshaped `IUsageEnforcer`; `ReserveAsync` now *persists* the hold (free tokens + IP aggregate + paid charge) before releasing the per-user lock, returns a `UsageReservation`; `SettleAsync` reconciles to actuals, `ReleaseAsync` refunds on failure. Decorator does reserve → call → settle, release-on-throw. `EfIpFreeUsageRepository.AddIssuedAsync` extended to signed/refundable deltas. Red→green: 50 concurrent reserves on a 1-call balance → **1 admitted, 49 → 402** (check-only code admitted all 50).
- **#2 — Unified `WithSessionLockAsync`.** Added to `ISessionStore<T>`, implemented once in `InMemorySessionStore<T>`; System Lab folded off its raw `GetLock`. All three orchestrators wrap mutating ops (Tutoring guidance; Prompt Lab + System Lab submit *and* chat). Red→green: concurrent turns broke alternation (Expected Assistant, Actual User) without the lock.
- **#3 — Pricing catalog + fail-fast.** Extracted `LlmPricingCatalog` (single model↔rate source). `LlmPricing` reads it and logs on the ceiling fallback. `AddValidatedProviderOptions` validates each provider's models via `Options.Validate(...).ValidateOnStart()` — unpriced model **fails app boot**. Negative test asserts `OptionsValidationException`.
- **Verification:** `dotnet test` — **257 passed, 0 failed** (from 241). Nothing committed by the assistant; user committed #1 and #2 themselves between steps.

## Open Questions / Next Steps
- **In-memory sessions and their per-session locks never evict** — unbounded memory growth. Flagged in `context.md` as a follow-up; not addressed.
- **`RowVersion` multi-instance hardening** — the reservation fix is correct in-process (singleton `UserUsageLock`); cross-instance optimistic-concurrency retry on `CreditBalance.RowVersion` remains deferred.
- Demoted candidate: provider-selection consistency / spend allow-list (largely intended design; only minor required-vs-optional inconsistency across surfaces).

## Artifacts
- **Plan:** `~/.claude/plans/alright-circle-back-to-validated-lake.md` (item #1 plan, approved).
- **#1 files:** `CodeSmith.Core/Models/Usage/UsageReservation.cs` (new), `Core/Interfaces/IUsageEnforcer.cs`, `Infrastructure/Services/Usage/UsageEnforcer.cs`, `.../Decorators/UsageEnforcingLlmService.cs`, `Core/Interfaces/IIpFreeUsageRepository.cs` + `Infrastructure/.../EfIpFreeUsageRepository.cs`; tests `UsageEnforcerTests.cs`, `UsageEnforcingLlmServiceTests.cs` (new).
- **#2 files:** `Core/Interfaces/ISessionStore.cs`, `Infrastructure/Services/InMemorySessionStore.cs`, `TutoringService.cs`, `PromptLab/PromptLabService.cs`, `SystemLab/SystemLabService.cs`; tests `InMemorySessionStoreTests.cs`, `AnthropicServiceTests.cs` (holds `TutoringServiceTests`), passthrough config in `PromptLabServiceTests`/`SystemLabServiceTests`.
- **#3 files:** `Infrastructure/Services/Usage/LlmPricingCatalog.cs` (new), `LlmPricing.cs`, `DependencyInjection/ServiceCollectionExtensions.cs`; tests `LlmPricingCatalogTests.cs` (new), `ProviderOptionsValidationTests.cs` (new), `LlmPricingTests.cs`.
- **Docs:** `context.md` (root), `CLAUDE.md` (root) — both reconciled to current state.
- **Verify command:** `dotnet test CodeSmith.Tests/CodeSmith.Tests.csproj`.
