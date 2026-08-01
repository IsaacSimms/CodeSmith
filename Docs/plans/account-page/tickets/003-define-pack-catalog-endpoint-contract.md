---
id: 003
title: Define the pack-catalog endpoint contract
type: grilling
status: closed
blocked_by: []
---

## Question

Settled constraint 3 fixes the source (Stripe Price objects for the allow-listed
`StripeOptions.PriceIds`) but not the contract or its failure behavior.

Resolve:

- Response DTO. Price id, amount, currency — and what supplies the **display name**? Stripe's
  `nickname` on the Price, or the Product name (which requires expanding `product` on the
  retrieve call)?
- Ordering. Amount ascending, config order, or Stripe's return order?
- Caching. What duration, and what cache — in-memory on the API instance, or none? Prices change
  rarely; a stale price is the exact failure mode this endpoint exists to prevent, so the two
  pull against each other.
- **Failure behavior when Stripe is unreachable.** Does `/api/billing/packs` 502, return an empty
  list, or serve stale cache? This determines whether the account page's purchase section can
  fail independently of the rest of the page.
- What happens to a configured price id that no longer exists in Stripe, or is inactive — skip it,
  or fail the whole response?
- Does a non-USD price in the allow-list get filtered? The webhook already ignores non-USD
  (`StripeBillingService.cs:104-108`), so advertising one would sell something that cannot credit.
- Auth: `[Authorize]` or anonymous? Pack pricing is arguably public marketing information.

## Answer

### Contract

```
GET /api/billing/packs          [Authorize]
200 → [
  { "priceId": string, "name": string, "amount": decimal, "currency": string }
]
502 → Stripe transport / API failure (and no successful response is cached yet, or cache miss
      under the load-only policy below)
```

Source remains settled constraint 3: retrieve Stripe Price objects for each id in
`StripeOptions.PriceIds`, expand `product` for the display name. `PriceIds` remains the
purchasability gate (checkout already enforces it).

### Decisions

| # | Decision | Reasoning |
|---|---|---|
| 1 | Stripe unreachable → **502** | Purchase is a composed query; it can fail alone without blanking balance/ledger/quota. Empty `200 []` would disguise outage as "we sell nothing." Stale-as-shield was rejected — cache is not a resilience layer at near-zero payer scale |
| 2 | Cache successes **5 minutes in-memory per API process** | Load reduction only (SPA remounts / multi-query layout). Not used to answer when Stripe is down. Each instance warms itself; no distributed cache |
| 3 | Unusable allow-list entries → **skip + warn log**, return the rest | Missing Price, inactive Price, non-USD, or blank/whitespace Product name. One retired `price_…` left in appsettings must not 502 the whole catalog. Single operator, rare Stripe churn — fail-loud is optional later with deploy-time validation |
| 4 | All ids unusable → **200 `[]`**, not 502 | Config/catalog problem distinct from Stripe transport failure. SPA can show "no packs available" without a transport error |
| 5 | Display name = **Stripe Product name** | Normal catalog shape (Product = what, Price = how much). Price `nickname` is optional and often empty. Requires expand `product` on retrieve |
| 6 | Order = **`StripeOptions.PriceIds` array order** | Operator-owned sequence already in appsettings; skips close gaps (no sparse holes). Amount sort and badge metadata stay map fog |
| 7 | **`[Authorize]`** | Matches balance/ledger/checkout. Unauth `/account` is a sign-in shell, not a public storefront. Avoids an anonymous Stripe-proxy on the secret-keyed API |
| 8 | DTO amount = **decimal major units** + **ISO `currency`** | Aligns with `paidCreditsUsd`, ledger `amountUsd`, and webhook `amount_total / 100m`. Non-USD rows never appear (decision 3), so `currency` is always `"usd"` in practice but stays on the wire for self-description. Response is a bare JSON array, not a wrapper object |

### Consequences for the map

- Purchase section on the account page must treat packs as an **independent** TanStack Query:
  section-level error on 502; empty-state on `[]`; no coupling to balance/ledger failure.
- Implements need `IMemoryCache` (or equivalent) for the first time in this API if not already
  registered — cache only successful lists, key stable for the process, TTL 5 minutes.
- No new fog graduated. Config-driven badges / non-price ordering remains in **Not yet specified**.
- Does not block or unblock other open tickets; 007 remains gated only on 002 (already closed).
