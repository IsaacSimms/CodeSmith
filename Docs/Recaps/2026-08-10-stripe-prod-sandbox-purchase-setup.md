# Stripe Prod Sandbox Purchase Setup

**Date:** 2026-08-10
**Type:** ops
**Environment / Systems:** Azure prod (`rg-codesmith-prod-centralus-001`, Container App `ca-codesmith-api-001`, SWA `yellow-sand-03abd5710.7.azurestaticapps.net`); Stripe test/sandbox; Key Vault `kvcodesmithprod001`

## TL;DR
Account Credits packs failed on prod because the API never received `Stripe__SecretKey` (and related billing env). Grill locked an ops-only fix on Azure + Stripe test mode; user wired secret key, webhook endpoint, webhook secret, and corrected Success/Cancel URLs. Full sandbox purchase path is ready to use; no app code changes.

## Context & Goal
Account page wayfinder work was already done. On the live Account page, paid balance and history loaded, but Credits pack catalog showed a generic error. Stripe catalog already had $5 / $10 / $25 packs in test mode; KV held Stripe secrets. Goal: finish the **project-side** purchase workflow against **prod Azure** while keeping Stripe in **sandbox**, without a plan artifact.

## Key Points Explored
- Packs call Stripe (`GET /api/billing/packs`); balance/ledger/quota do not — matches “balance works, packs die” and **502** when `SecretKey` is empty.
- Repo has no in-process Key Vault SDK; config is **Container App env vars** (double-underscore names bind to `StripeOptions`).
- KV names (`Stripe-SecretKey`, `Stripe-WebhookSecret`) ≠ app binding names (`Stripe__SecretKey`, `Stripe__WebhookSecret`).
- CA already had `Stripe__SuccessUrl` / `Stripe__CancelUrl` pointing at **deleted** SPA routes `/billing/success|cancel` (pre–account-page).
- Shipped allow-list Price ids still match Dashboard test Prices (`price_1Tnt9n…`, `price_1TntCS…`, `price_1TntDO…`). Product ids (`prod_…`) are not used.
- Dashboard webhook must target API host + **`/api/billing/webhook`**, not the Application Url root. CLI/`appsettings.Development` `whsec` is not valid for that endpoint.
- API host locked as `https://ca-codesmith-api-001.icysea-31eca31b.centralus.azurecontainerapps.io`.
- Test secret key was pasted into chat during setup — treat as exposed and rotate.

## Decisions & Outcomes
| Decision | Choice |
|---|---|
| Where first | Azure prod only |
| Stripe mode | Test/sandbox only (no live cutover) |
| Config mechanism | Existing CA env injection |
| Price ids | Keep shipped three `price_…` values |
| Webhook | Dashboard event destination, `checkout.session.completed` |
| Return URLs | SWA `/account?checkout=success\|cancel` |
| Done bar | Full test purchase → TopUp + balance (user confirmed setup complete through config; verification buy in good place) |

**Ops completed by user:**
- `Stripe__SecretKey` set on CA (`sk_test_…`)
- `Stripe__WebhookSecret` set from new endpoint signing secret
- Success/Cancel updated to account checkout query URLs
- Stripe sandbox event destination created →  
  `https://ca-codesmith-api-001.icysea-31eca31b.centralus.azurecontainerapps.io/api/billing/webhook`

No application code or deploy-from-repo required for this pass.

## Open Questions / Next Steps
- **Rotate** the test secret key that appeared in chat/screenshots; update CA (and KV if used as source of truth).
- Prefer secret **references** (KV → CA secret → env) over long-lived plaintext manual env values, matching provider-key pattern where possible.
- Optional: align Stripe product marketing copy (“N CodeSmith credits”) with real model (**USD prepaid `PaidCreditsBalance`**).
- Live-mode cutover later: new keys, confirm live Price ids, new webhook endpoint + `whsec`, real charges — separate pass.
- If packs or credit ever regress: check active CA revision has all three Stripe secrets/URLs; Stripe delivery logs for 400 (sig) vs 500 (transient).

## Artifacts
- Account wayfinder (prior): `Docs/plans/account-page/`
- Billing options: `CodeSmith.Infrastructure/Configuration/StripeOptions.cs`
- Pack/checkout/webhook: `CodeSmith.Infrastructure/Billing/StripeBillingService.cs`
- Shipped Price allow-list: `CodeSmith.Api/appsettings.json` → `Stripe:PriceIds`
- Prior E2E (local API + Azure SQL + `stripe listen`): `Docs/Recaps/2026-07-08-stripe-billing-azure-e2e-testing.md`
- SWA host: `https://yellow-sand-03abd5710.7.azurestaticapps.net`
- API host: `https://ca-codesmith-api-001.icysea-31eca31b.centralus.azurecontainerapps.io`
