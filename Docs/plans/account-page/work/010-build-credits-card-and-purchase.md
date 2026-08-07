---
id: 010
title: Build the credits card and pack purchase
status: done
implements: [003, 005]
depends_on: [007, 008]
---

## Goal

Build the credits half of the wallet row: paid balance, the purchasable pack list, and the checkout
buttons, with the `#credits` anchor that paywall links target.

## Constraints

- Credits sits in the `grid sm:grid-cols-2` wallet row, top-left, with `anchorId="credits"` —
  [Choose the account page layout](../tickets/005-choose-account-page-layout.md) #1
- The card feeds `AccountSection` the **balance** query state only. A `/api/billing/packs` 502
  degrades the pack area alone, inline, with a retry — taking the card down would hide a balance the
  server already returned — #3
- Packs render in the order the endpoint returns (`StripeOptions.PriceIds` order); `200 []` is an
  empty state ("no packs available"), not an error —
  [Define the pack-catalog endpoint contract](../tickets/003-define-pack-catalog-endpoint-contract.md) #4, #6
- Balance renders 2dp, `$0.00` only for a true zero, `< $0.01` for a spendable sub-cent remainder —
  [Decide free-covered ledger row semantics](../tickets/008-decide-free-covered-ledger-row-semantics.md) #11
- A brand-new user renders uniformly: `$0.00` beside the price buttons, no new-user branch — #7
- Buy posts to the existing `POST /api/billing/checkout` with the pack's `priceId` and redirects to
  the returned URL. The baseline snapshot that path also needs lands in
  [Build the post-checkout flow](013-build-post-checkout-flow.md)
- Uses the shared hooks from [Add account data hooks](008-add-account-data-hooks-and-invalidation.md);
  no local fetching

## Acceptance criteria

- `/account` renders the credits card with the paid balance and one button per pack showing the
  Product name and formatted amount.
- A test proves a packs 502 renders an inline error with a retry **while the balance stays visible**,
  and that retry refetches only packs.
- A test proves `200 []` renders the empty state, not the error state.
- A test proves clicking a pack posts the matching `priceId` and follows the returned checkout URL.
- `/account#credits` scrolls to and rings this card.
- `npm test` passes.
