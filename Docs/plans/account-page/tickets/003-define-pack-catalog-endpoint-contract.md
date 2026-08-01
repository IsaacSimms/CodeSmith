---
id: 003
title: Define the pack-catalog endpoint contract
type: grilling
status: open
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

<!-- Empty until resolved. -->
