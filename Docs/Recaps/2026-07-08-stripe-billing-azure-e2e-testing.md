# Stripe Billing — Azure E2E Testing

**Date:** 2026-07-08
**Type:** verification
**Environment / Systems:** CodeSmith (.NET 8), Azure SQL (`db-codesmith-prod-centralus-001`), Stripe test mode + Stripe CLI v1.43.2, Bruno, local API on `http://localhost:5175`

## TL;DR
Picked up from the 2026-07-07 Stripe billing implementation handoff, grilled the live-test plan (chose Azure SQL over LocalDB), fixed a dev config key mismatch, and ran the full Checkout → webhook → credit → idempotency → paid-debit flow against prod Azure SQL. All paths passed. The only cosmetic issue: Stripe redirects to a non-existent `https://localhost:7111/billing/success` page.

## Context & Goal
The prepaid-credits billing module was implemented and green at 324/324 unit tests, but never exercised against a real Stripe account or live SQL database. This thread's goal: understand blockers, decide the test architecture via grill-me, execute live E2E on Azure (the actual deploy target), and confirm the billing ↔ enforcer seam (credits written by billing, debits by enforcer only).

## Key Points Explored
- **Grill-me decisions:** Azure SQL (not LocalDB); dual config (user-secrets + `appsettings.Development.json`); `stripe listen` on HTTP 5175 with per-session `whsec_...` sync; verification plan D (Bruno + Portal SQL + `stripe events resend`); stretch-goal enforcer test after billing passes; test identity `11111111-1111-1111-1111-111111111111`.
- **"Blocker" reframed as config bug:** `ServiceCollectionExtensions` reads `ConnectionStrings:CodeSmithDb`, but `appsettings.Development.json` had the Azure server name as the key (`sql-codesmith-prod-centralus-001`) with a dead SQL-login string. User-secrets already had the correct Entra-auth `CodeSmithDb` value — runtime worked; the file was misleading.
- **Free quota is tokens, not USD:** `CreditBalances.FreeTokensUsedInWindow` / `FreeQuotaMax` + 48h `FirstSeenUtc` window + `IpFreeUsages` per-IP cap (60k). `PaidCreditsBalance` is separate. Exhausting free ≠ zeroing the used-token column; `20000/20000` means **0 remaining**.
- **Portal Query Editor UX:** Multi-statement scripts only show the last result set by default; combined `UNION ALL` query needed to see balance + TopUp + dedup in one grid.
- **Post-checkout redirect:** `SuccessUrl`/`CancelUrl` in `appsettings.json` point to `https://localhost:7111/billing/success` — no route exists (backend-only increment) and API was on HTTP 5175. Payment and webhook succeeded before redirect failed.

## Decisions & Outcomes
- **Config fix:** `appsettings.Development.json` — renamed connection string key to `CodeSmithDb`, switched to `Authentication=Active Directory Default`, removed stale `CloudSA67f19294` SQL login entry.
- **Secrets:** `Stripe:SecretKey` added to user-secrets (not committed). Webhook secret from `stripe listen` matched the static dev config (`whsec_8b016...`) — no API restart needed for webhook secret.
- **Migration:** `20260707051657_AddStripeBilling` applied to Azure (user, outside agent terminal).
- **Checkout E2E:** `POST /api/billing/checkout` with `price_1Tnt9nRzQXBJQm3BK0llW9f7` ($5) → browser payment with `4242...` → `checkout.session.completed` webhook returned `Credited`.
- **Balance:** `-0.249708` → **`4.750292`** (+$5 on prior negative paid balance).
- **Ledger:** `TopUp` row, `$5.00`, `Billing:TopUp`, `objectId` in session metadata confirmed on resend.
- **Idempotency:** `stripe events resend evt_1TqndURzQXBJQm3BVrXX1c20` — balance unchanged, single TopUp row, single `ProcessedStripeEvents` row.
- **Portal SQL:** Balance + TopUp + dedup all visible in `db-codesmith-prod-centralus-001`.
- **Enforcer crossover (stretch):** SQL script expired 48h window + maxed free tokens; `POST /api/session` (Easy/CSharp/Xai) debited **`$0.004225`** from paid balance (`4.750292` → `4.746067`), new `Spend` / `Tutoring:ProblemGeneration` ledger row.

## Open Questions / Next Steps
- **Dev redirect URLs:** Point `Stripe:SuccessUrl`/`CancelUrl` in `appsettings.Development.json` to something reachable (e.g. `http://localhost:5175/swagger`) until a frontend billing success page exists.
- **Commit** the `appsettings.Development.json` connection-string fix (no Stripe secret in file).
- **Negative paths** from the handoff (tampered signature → 400, unknown priceId → 400, no auth → 401) were not run this session — optional cleanup.
- **Production:** Real frontend success/cancel routes when billing UI is built.

## Artifacts
- **Recap inputs:** `Docs/Recaps/2026-07-07-stripe-billing-implementation.md`, `Docs/Handoffs.Agent/2026-07-07-stripe-billing-testing-handoff.md`
- **Edited:** `CodeSmith.Api/appsettings.Development.json` — `ConnectionStrings:CodeSmithDb` with Entra auth
- **User-secrets:** `ConnectionStrings:CodeSmithDb`, `Stripe:SecretKey`
- **Stripe event (checkout):** `evt_1TqndURzQXBJQm3BVrXX1c20`; session `cs_test_a15t4wTvlVXx7xFe6fTuKAcFpkiLbbYWP166Po98yX2y0kEFLRryosDdvE`
- **Portal verification query (single result set):**
  ```sql
  DECLARE @ObjectId NVARCHAR(128) = '11111111-1111-1111-1111-111111111111';
  SELECT 'Balance' AS [Check], b.PaidCreditsBalance AS Amount, NULL AS Feature, NULL AS EventId, NULL AS [When]
  FROM CreditBalances b WHERE b.ObjectId = @ObjectId
  UNION ALL
  SELECT 'TopUp', l.CostUsd, l.Feature, NULL, l.TimestampUtc
  FROM UsageLedgerEntries l WHERE l.ObjectId = @ObjectId AND l.Type = 1
  UNION ALL
  SELECT 'Dedup', NULL, NULL, p.EventId, p.ProcessedUtc
  FROM ProcessedStripeEvents p WHERE p.EventId = 'evt_1TqndURzQXBJQm3BVrXX1c20';
  ```
- **Free-quota kill script:** expire `FirstSeenUtc` (−49h), set `FreeTokensUsedInWindow = FreeQuotaMax`, cap `IpFreeUsages` for local IPs
- **Commands used:** `stripe listen --forward-to http://localhost:5175/api/billing/webhook`, `stripe events resend evt_...`, `dotnet ef database update`