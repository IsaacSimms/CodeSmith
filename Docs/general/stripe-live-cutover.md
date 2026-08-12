# Stripe live cutover (sandbox → live on Azure prod)

**Date:** 2026-08-13  
**Audience:** You (Stripe Dashboard + Azure Container App env)  
**Scope:** Activate Stripe live mode and point **prod API only** at live keys, live Prices, and a live webhook.  
**Prerequisite:** Sandbox purchase path already works on prod (see `Docs/Recaps/2026-08-10-stripe-prod-sandbox-purchase-setup.md`). Success/Cancel URLs already point at `www.code-smith.cc`.

**Repo code:** **None** — ops/config only. Do not commit live Price ids or secrets.

---

## Locked decisions

| Decision | Choice |
|----------|--------|
| Where live lands | **Azure prod only** (`ca-codesmith-api-001`). No dual live/test on prod. |
| Local / rare localhost | Stays **test mode** (`sk_test` + shipped `appsettings.json` test `PriceIds`) if you ever run it again |
| Account state at start | Live **not** activated yet — activation is Phase 0 |
| Catalog | Same packs as sandbox: **one-time USD** $5 / $10 / $25. Recreate or copy into **live**; new `price_…` ids |
| Price ids in git | **Unchanged** (test ids in `appsettings.json`). Live ids only on CA env (`Stripe__PriceIds__0..2`) |
| Secrets mechanism | **CA env vars** (manual entry), same pattern as sandbox pass |
| Webhook | **New live-mode** endpoint → existing API URL + `/api/billing/webhook`; event **`checkout.session.completed` only** |
| Return URLs | **Leave as-is** (already www) |
| Publishable key (`pk_…`) | **Not used** by CodeSmith (hosted Checkout + secret key server-side only) |
| Done bar | One real **$5** purchase on prod → TopUp + balance; optional Dashboard refund |
| Users / traffic | None expected — this is a straight replace of test config with live, not a phased migration |

### What must change (fan-out)

| # | Where | What |
|---|--------|------|
| 1 | Stripe Dashboard (live) | Finish account activation (payouts / business profile) |
| 2 | Stripe Dashboard (live) | Products + Prices ($5 / $10 / $25 one-time USD) — copy from test if offered, else recreate |
| 3 | Stripe Dashboard (live) | Webhook endpoint → API `/api/billing/webhook`, `checkout.session.completed` |
| 4 | Azure Container App | `Stripe__SecretKey` = `sk_live_…` |
| 5 | Azure Container App | `Stripe__WebhookSecret` = live endpoint `whsec_…` |
| 6 | Azure Container App | `Stripe__PriceIds__0` / `__1` / `__2` = **live** `price_…` ids |
| 7 | You | Smoke: packs load → checkout → pay → balance/ledger |

**Do not change for this cut:** `Stripe__SuccessUrl`, `Stripe__CancelUrl`, webhook path, API hostname, SPA, Entra, CORS, DB schema.

**Explicitly out of scope:** Stripe Customer objects, Customer Portal, subscriptions, refund webhooks, Key Vault rewiring, committing live ids to the repo, multi-instance billing changes.

---

## Current anchors (prod)

Confirm in portal if anything drifted:

| Surface | Value |
|---------|--------|
| Resource group | `rg-codesmith-prod-centralus-001` |
| API Container App | `ca-codesmith-api-001` |
| API public host | `https://ca-codesmith-api-001.icysea-31eca31b.centralus.azurecontainerapps.io` |
| Webhook URL (unchanged) | `https://ca-codesmith-api-001.icysea-31eca31b.centralus.azurecontainerapps.io/api/billing/webhook` |
| SPA | `https://www.code-smith.cc` |
| Success URL (already set) | `https://www.code-smith.cc/account?checkout=success` |
| Cancel URL (already set) | `https://www.code-smith.cc/account?checkout=cancel` |
| Key Vault (optional mirror) | `kvcodesmithprod001` — names `Stripe-SecretKey` / `Stripe-WebhookSecret` ≠ app bind names |
| App bind names | `Stripe__SecretKey`, `Stripe__WebhookSecret`, `Stripe__PriceIds__N`, `Stripe__SuccessUrl`, `Stripe__CancelUrl` |
| Shipped **test** Price ids (repo only; not for live) | See `CodeSmith.Api/appsettings.json` → `Stripe:PriceIds` |

### App behavior (why these knobs)

| Call | Needs |
|------|--------|
| `GET /api/billing/packs` | `SecretKey` + allow-listed Price ids that **exist in the same mode** as the key |
| `POST /api/billing/checkout` | Same + `SuccessUrl` / `CancelUrl` |
| `POST /api/billing/webhook` | `WebhookSecret` matching the endpoint that signed the event (live vs test secrets differ) |
| Balance / ledger / quota | No Stripe — DB only |

Mode mismatch symptoms:

| Symptom | Likely cause |
|---------|----------------|
| Packs **502** | Missing/invalid `Stripe__SecretKey` |
| Packs **200 []** | Live key + old test `price_…` ids (unusable → skipped) |
| Checkout **400** unknown price | `priceId` not in CA allow-list |
| Pay succeeds, balance never moves | Wrong `whsec`, test webhook only, or webhook URL wrong |
| Webhook **400** in Stripe delivery log | Signature / `Stripe__WebhookSecret` mismatch |
| Webhook **500** | Transient DB — Stripe retries; check API logs |

---

## Order of operations

Do not put live `sk_live` on the CA until live Prices exist and the live webhook signing secret is ready — or packs/checkout will break mid-flight with no users to care, but you’ll confuse your own smoke.

1. Stripe: **Switch to live account** → finish **activation / Setup guide**  
2. Stripe live: create or copy **Products + Prices** → record three `price_…` ids  
3. Stripe live: create **webhook endpoint** → copy **Signing secret** (`whsec_…`)  
4. Stripe live: copy **Secret key** (`sk_live_…`) from Developers → API keys  
5. Azure CA: set `Stripe__SecretKey`, `Stripe__WebhookSecret`, `Stripe__PriceIds__0..2` (new revision)  
6. Smoke test (below)  
7. Optional: disable/delete old **test** webhook endpoint; rotate any test key that was ever pasted into chat  
8. Optional: mirror secrets into Key Vault for backup (still bind via CA env unless you wire secret refs)

---

## Phase 0 — Activate live account

1. Open [Stripe Dashboard](https://dashboard.stripe.com) → top right **Switch to live account** (leave sandbox).  
2. Banner should **not** say you’re testing in a sandbox.  
3. Complete **Setup guide** / activation until live charges and payouts are allowed:
   - Business details  
   - Bank account for payouts  
   - Identity / verification as prompted  
   - Public business info / support email as prompted  
4. Stop here if Stripe still blocks live charges — do not flip CA secrets yet.

**Pass:** Dashboard is in **live** mode and activation is complete (or Stripe explicitly allows test charges in live — you want real card capability for the $5 smoke).

---

## Phase 1 — Live Products and Prices

Sandbox catalog does **not** share ids with live. Carry **amounts and names**; accept new ids.

1. Still in **live** mode.  
2. **Product catalog** → recreate (or use any Dashboard **copy from test** action if shown) three **one-time** Prices:

   | Pack (display) | Amount | Currency | Type |
   |----------------|--------|----------|------|
   | Match your test Product names | **$5.00** | USD | One-time |
   | | **$10.00** | USD | One-time |
   | | **$25.00** | USD | One-time |

3. Open each Price → copy the **Price id** (`price_…`).  
4. Decide CA order (this is display order on Account). Recommended match to historical intent:

   | Env var | Pack |
   |---------|------|
   | `Stripe__PriceIds__0` | $25 (or whatever you want first) |
   | `Stripe__PriceIds__1` | $10 |
   | `Stripe__PriceIds__2` | $5 |

   Repo test order today is not sacred for live; pick a stable order and document the three ids in your password manager / notes (not in git).

**Pass:** Three live `price_…` ids, all active, USD, one-time, Product name non-blank (blank names are skipped by `/packs`).

---

## Phase 2 — Live webhook endpoint

1. Live mode → **Developers** → **Webhooks** (or Workbench event destinations).  
2. **Add endpoint**:
   - **URL:**  
     `https://ca-codesmith-api-001.icysea-31eca31b.centralus.azurecontainerapps.io/api/billing/webhook`  
   - **Events:** `checkout.session.completed` only  
3. Open the endpoint → **Signing secret** → **Reveal** → copy `whsec_…`.  
4. Leave the old **test-mode** endpoint alone until after smoke (or delete it now if you prefer less clutter). Test endpoints never see live events.

**Pass:** Live endpoint exists, URL path ends with `/api/billing/webhook`, signing secret saved for Phase 3.

---

## Phase 3 — Live secret key

1. Live mode → **Developers** → **API keys**.  
2. **Secret key** → Reveal / create → copy `sk_live_…`.  
3. Do **not** put this in chat, git, screenshots, or `appsettings*.json`.  
4. Publishable key (`pk_live_…`) — ignore for CodeSmith.

---

## Phase 4 — Container App env

Resource: `ca-codesmith-api-001` in `rg-codesmith-prod-centralus-001` → **Containers** / **Environment variables** (exact portal labels vary).

Set or replace:

| Name | Value |
|------|--------|
| `Stripe__SecretKey` | `sk_live_…` |
| `Stripe__WebhookSecret` | live endpoint `whsec_…` |
| `Stripe__PriceIds__0` | live `price_…` |
| `Stripe__PriceIds__1` | live `price_…` |
| `Stripe__PriceIds__2` | live `price_…` |

**Leave unchanged:**

| Name | Expected value |
|------|----------------|
| `Stripe__SuccessUrl` | `https://www.code-smith.cc/account?checkout=success` |
| `Stripe__CancelUrl` | `https://www.code-smith.cc/account?checkout=cancel` |

Save → wait for new revision **Running**.

**Array binding note:** ASP.NET Core binds `Stripe__PriceIds__0`, `__1`, `__2` to `StripeOptions.PriceIds`. If a single comma-joined value was ever used, prefer indexed `__N` form. After revision is up, empty/wrong allow-list shows up as empty packs, not a boot failure (`StripeOptions` is not ValidateOnStart).

**Pass:** Active revision shows all six Stripe-related vars (four secrets/ids + two URLs) with live key prefix `sk_live_` and three live price ids.

---

## Phase 5 — Smoke test

Use a real browser session on prod (signed-in Entra user).

| # | Action | Expect |
|---|--------|--------|
| 1 | `https://www.code-smith.cc` → sign in | Account loads |
| 2 | Account → credit packs | Three packs, correct USD amounts and names (**not** empty, not generic error) |
| 3 | Buy **$5** pack | Redirect to Stripe Checkout (**live** — no “test mode” badge) |
| 4 | Pay with your card | Stripe success → redirect to `/account?checkout=success` |
| 5 | Account balance / history | Paid balance **+$5**; ledger **TopUp** row (webhook may lag a few seconds; page polls) |
| 6 | Stripe live Dashboard → Payments | Succeeded $5 Payment |
| 7 | Stripe live → Webhook endpoint → deliveries | `checkout.session.completed` **2xx** |
| 8 | Optional | Refund the $5 in Stripe Dashboard (app does **not** reverse credits on refund today — if you refund, manually expect balance to stay credited unless you adjust DB; prefer keeping the $5 as owner credits) |

**Refund honesty:** CodeSmith credits on `checkout.session.completed` only. A Dashboard refund does **not** debit `PaidCreditsBalance`. For a clean smoke, either keep the $5 as real prepaid balance or accept orphan credits after refund. Do not build refund handling in this ops pass.

**Pass:** Steps 1–7 green.

---

## Phase 6 — Cleanup (optional)

- Disable or delete the **test** webhook that pointed at the same API URL (avoids “which secret is this?” later).  
- Rotate any `sk_test` that was pasted into chat/screenshots (prior sandbox setup).  
- Mirror `sk_live` / `whsec` into Key Vault `kvcodesmithprod001` if you want a backup copy — **does not** replace CA env until you configure secret references.  
- Do **not** delete test Products in sandbox; keep them for any future local test.

---

## Rollback (re-enable sandbox on prod)

Only if live is broken and you need packs again quickly:

1. CA: restore `Stripe__SecretKey` = `sk_test_…`  
2. CA: restore `Stripe__WebhookSecret` = **test** endpoint `whsec_…` (re-create test endpoint if deleted)  
3. CA: remove live `Stripe__PriceIds__N` **or** set them back to the three test ids from `appsettings.json`  
4. New revision → smoke a test-mode card (`4242…`)  

Success/Cancel URLs stay on www either way.

---

## Troubleshooting

| Observation | Check |
|-------------|--------|
| Still see Stripe “test mode” on Checkout | CA still has `sk_test` or browser hit a stale revision |
| Packs empty array | Price ids test/live mismatch; inactive Price; non-USD; blank Product name |
| Packs 502 | `Stripe__SecretKey` empty/wrong; Stripe API error in logs |
| Checkout URL ok, no credit | Webhook delivery log: 400 → fix `WebhookSecret`; 404 → wrong URL; 500 → API/DB |
| Credit doubled | Should not — `ProcessedStripeEvent` dedup; if it does, capture event id + ledger rows before touching DB |
| Success redirect wrong host | `Stripe__SuccessUrl` / `CancelUrl` drifted from www |

---

## Related docs

- `Docs/Recaps/2026-08-10-stripe-prod-sandbox-purchase-setup.md` — sandbox-on-prod wiring  
- `Docs/general/custom-domain-cloudflare-swa.md` — Success/Cancel → www  
- `context.md` — Billing module, webhook contract, `StripeOptions`  
- `CodeSmith.Infrastructure/Configuration/StripeOptions.cs` — bind shape  
- `CodeSmith.Infrastructure/Billing/StripeBillingService.cs` — checkout / webhook / packs  
