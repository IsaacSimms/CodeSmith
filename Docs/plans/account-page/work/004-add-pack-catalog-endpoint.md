---
id: 004
title: Add the pack-catalog endpoint
status: todo
implements: [003]
depends_on: []
---

## Goal

Ship `GET /api/billing/packs`, reading Stripe Price objects for the allow-listed ids so displayed
prices can never drift from charged prices, with a short in-memory success cache.

## Constraints

- `GET /api/billing/packs` `[Authorize]`, returning a bare JSON array of
  `{ priceId, name, amount, currency }` —
  [Define the pack-catalog endpoint contract](../tickets/003-define-pack-catalog-endpoint-contract.md) #7, #8
- Display name is the Stripe **Product** name, requiring `expand` on `product` — #5
- Order follows `StripeOptions.PriceIds` array order; skips close gaps — #6
- Cache successful lists 5 minutes in-memory per API process, for load reduction only; never used to
  answer while Stripe is down — #2
- Stripe unreachable → **502**, so the purchase section can fail independently of balance, ledger,
  and quota — #1
- Unusable allow-list entries (missing, inactive, non-USD, blank Product name) → skip and warn-log,
  return the rest; all ids unusable → `200 []`, not 502 — #3, #4
- Amount is decimal major units, currency is the ISO code — #8
- `StripeOptions.PriceIds` remains the purchasability gate; checkout's existing enforcement is
  unchanged

## Acceptance criteria

- `GET /api/billing/packs` returns the allow-listed packs in `PriceIds` order with Product names,
  decimal amounts, and currency; 401 unauthenticated.
- A test proves a missing / inactive / non-USD price is skipped and the remaining packs return 200.
- A test proves all-unusable returns `200 []`, and that a Stripe transport failure returns 502.
- A test proves a second call within the TTL does not hit Stripe, and that a failure is not cached.
- `IMemoryCache` (or equivalent) is registered if it was not already.
- `context.md` and `CLAUDE.md` document the endpoint.
- `dotnet test` passes.
