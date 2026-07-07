# Stripe Prepaid Credits Billing — Implementation

**Date:** 2026-07-07
**Type:** implementation
**Environment / Systems:** CodeSmith (.NET 8 Clean Architecture), EF Core + Azure SQL, Stripe.net 52 (test mode)

## TL;DR
Grilled a prior Stripe billing design (found seven load-bearing flaws in the "locked" handoff), replanned it, then implemented the full prepaid-credits module across four phases. Backend is complete and green: `dotnet build` clean, `dotnet test` 275/275. Uncommitted, pending review and live Stripe CLI testing.

## Context & Goal
`PaidCreditsBalance` existed and was debited by `UsageEnforcer`, but there was no way to fund it. The goal: let authenticated users buy credit packs via Stripe Checkout and credit their balance through a signature-verified, idempotent webhook — in a billing module that never touches usage enforcement. A prior thread produced a design handoff claiming "all decisions locked, no new migrations." This thread grilled that, corrected it, and built it.

## Key Points Explored
- **Grill-me found seven contradictions** between the handoff and the real code: (1) idempotency vs. "no migrations" — Stripe is at-least-once, additive credits aren't idempotent; (2) webhook races the enforcer's `IUserUsageLock`-guarded balance writes; (3) `UsageLedgerEntry` has no shape for a top-up (LLM-call fields, `AiProvider` has no neutral value, `CostUsd` is a debit); (4) signature verification is a static Stripe call, untestable without a seam; (5) a user can pay before their first LLM call → no balance row; (6) unvalidated `priceId` + currency assumption; (7) returning entities on `/ledger` leaks `ProviderCostUsd` (margin).
- **Atomicity refinement mid-implementation:** "insert-first dedup" + "credit" as two saves has an opposite failure (mark-processed without crediting). Resolved by committing dedup + credit + ledger in **one `SaveChangesAsync`** inside a deep store.

## Decisions & Outcomes
- **Phase 0:** `Stripe.net 52.*`; `StripeOptions` (secret key, webhook secret, price-ID allow-list, URLs); config sections added.
- **Phase 1 (data):** `LedgerEntryType {Spend=0, TopUp}` on `UsageLedgerEntry` (provider/model nullable); `ProcessedStripeEvent` dedup entity; `ICreditBalanceRepository.GetOrCreateAsync` + shared `CreditBalance.CreateNew`; `IStripeCreditStore`/`EfStripeCreditStore` (atomic idempotent credit with concurrency retry); migration `20260707051657_AddStripeBilling`. 4 tests.
- **Phase 2 (service):** `IBillingService` (Core, no Stripe types) + `WebhookResult`; `InvalidPriceException`/`WebhookSignatureException`; `IStripeEventReader`/`StripeEventReader` seam over `EventUtility`; `StripeBillingService` (allow-list checkout, idempotent webhook with USD/metadata/amount guards, balance/ledger reads). 9 tests.
- **Phase 3 (API):** `BillingController` (`checkout`/`balance`/`ledger` `[Authorize]`, `webhook` `[AllowAnonymous]` raw-body); Billing DTOs omitting `ProviderCostUsd`/`RowVersion`; two 400 exception mappers registered. Webhook contract: 400 bad sig / 200 processed-dup-ignored / 500 transient. 5 tests.
- **Two plan deviations (flagged live):** the deep `IStripeCreditStore` replaced the "credit via `SaveAsync` retry loop" (so `EfCreditBalanceRepository.SaveAsync` was left unchanged — the RowVersion rewrite was unnecessary); `freeTokensRemaining` on `/balance` deferred to avoid duplicating the enforcer's 48h-window rule.
- **Seam verified intact:** billing has zero references to `IUsageEnforcer`/`IUserUsageLock`/`ILlmService`.

## Open Questions / Next Steps
- **Uncommitted** — awaiting line-by-line review; then commit.
- **Live testing not yet done** (deliberately out of the "dotnet test only" delivery scope): set `Stripe:SecretKey` (currently blank), apply the migration to a real DB, run Stripe CLI (`stripe listen --forward-to https://localhost:5175/api/billing/webhook`), verify credit + idempotency + 400/401 negative paths.
- **Local connection-string mismatch (inferred):** DI reads `ConnectionStrings:CodeSmithDb`, but `appsettings.Development.json` defines `sql-codesmith-prod-centralus-001`.

## Artifacts
- **Testing handoff:** `Docs/Handoffs.Agent/2026-07-07-stripe-billing-testing-handoff.md` — full test plan for a fresh agent.
- **New code:** `CodeSmith.Infrastructure/Billing/` (service + event reader), `EfStripeCreditStore.cs`, `CodeSmith.Core/Interfaces/{IBillingService,IStripeCreditStore}.cs`, `CodeSmith.Core/Models/Usage/ProcessedStripeEvent.cs`, `CodeSmith.Api/Controllers/BillingController.cs`, `CodeSmith.Api/DTOs/Billing/*`, two exception mappers, migration `20260707051657_AddStripeBilling`.
- **Tests (18 new):** `CodeSmith.Tests/Infrastructure/Billing/{EfStripeCreditStoreTests,StripeBillingServiceTests}.cs`, `CodeSmith.Tests/Api/BillingControllerTests.cs`.
