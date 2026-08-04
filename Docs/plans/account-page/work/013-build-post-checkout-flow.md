---
id: 013
title: Build the post-checkout flow and delete BillingResultPage
status: todo
implements: [005, 006]
depends_on: [010]
---

## Goal

Land the Stripe return on `/account` and hold the user through the webhook lag without ever
implying a payment failed, replacing `BillingResultPage` entirely.

## Constraints

- `StripeOptions.SuccessUrl` / `CancelUrl` point at `/account?checkout=success|cancel`;
  `BillingResultPage.tsx` and its route are **deleted** — map constraint 4,
  [Decide top-up completion signal and polling bounds](../tickets/006-decide-topup-completion-signal.md) Consequences
- On successful `POST /api/billing/checkout`, **before** the Stripe redirect, snapshot known TopUp
  fingerprints `(TimestampUtc, AmountUsd)` into `sessionStorage` along with the pending intent — #2, #3
- On `/account?checkout=success`: accept the intent, `replace` the URL to strip the query, then poll
  the ledger immediately and every 2s for 30s. The banner and poll key off the pending flag, not the
  query string — #5, #9
- Completion is a **new TopUp ledger row** only; balance is display-only after that — #1
- Missing baseline falls through to the give-up path; never invent success from a time window — #4
- During poll: page-level inline banner "Applying your credits…" above the wallet row and below the
  identity header, transient, reserving no height. It does not block the rest of Account — #6,
  [Choose the account page layout](../tickets/005-choose-account-page-layout.md) Consequences
- On TopUp seen: brief "Credits added"; balance and ledger update — #7
- On give-up: "Payment received — credits may take a moment to appear. Refresh this page if the
  balance doesn't update." It must not imply the payment failed — #8
- `?checkout=cancel`: quiet dismissible "Checkout canceled — no charge was made.", strip the param,
  clear the baseline, no polling — #10
- Re-checkout while a poll runs is **allowed with a confirm** ("A purchase is still applying —
  continue?"); on confirm the new checkout overwrites baseline and pending — #11
- Top-up detection invalidates all three shared keys —
  [Add account data hooks](008-add-account-data-hooks-and-invalidation.md)

## Acceptance criteria

- `BillingResultPage.tsx`, its route, and its tests are gone; nothing references `/billing/result`.
- A test proves the checkout mutation writes the baseline fingerprints before redirecting.
- A test proves landing on `?checkout=success` strips the query with `replace` and that a refresh
  afterwards does not replay the flow.
- A test proves polling stops on the first TopUp row absent from the baseline, shows "Credits added",
  and refreshes balance and ledger.
- A test proves the 30s deadline renders the give-up copy, and that the copy contains no failure
  language.
- A test proves a webhook that already landed before first paint is still detected (baseline taken at
  checkout start, not at landing).
- A test proves a missing baseline goes to give-up rather than reporting success.
- A test proves `?checkout=cancel` renders the quiet notice, clears the baseline, and starts no poll.
- A test proves re-buying mid-poll prompts for confirmation and overwrites the baseline on confirm.
- `npm test` passes.
