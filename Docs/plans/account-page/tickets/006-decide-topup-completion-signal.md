---
id: 006
title: Decide top-up completion signal and polling bounds
type: grilling
status: closed
blocked_by: []
---

## Question

Settled constraint 4 lands the user on `/account?checkout=success` while the webhook is still in
flight. The redirect normally wins the race. Getting this wrong produces the duplicate-purchase
bug: user pays, sees an unchanged balance, concludes it failed, buys again.

Resolve:

- **What signals completion?** A changed `PaidCreditsBalance` is the obvious candidate but is
  ambiguous — a concurrent spend could move it, and a user who already had credits cannot
  distinguish. A new `TopUp` row in the ledger is unambiguous but costs a second query.
- Polling bounds: how many attempts, over what interval, before giving up?
- What does the user see **during** polling, and what does the terminal give-up state say? It must
  not imply failure, since the payment did succeed.
- Does the `?checkout=success` param get cleared from the URL after handling, so a refresh does
  not replay the state?
- What does `?checkout=cancel` render — a quiet notice, or nothing?
- Can a user open checkout again while a poll is still running?

Related fog: the *permanently* failed webhook is a separate question and is not in this ticket.

## Answer

### Contract (client post-checkout flow)

1. **On successful `POST /api/billing/checkout` (before Stripe redirect):** snapshot known TopUp
   fingerprints into `sessionStorage`. Fingerprint a row as `(TimestampUtc, AmountUsd)` for each
   ledger entry with `Type === TopUp` (ledger DTO has no row id). Also mark post-checkout pending
   intent as needed for the return path.
2. **On `/account?checkout=success`:** accept post-checkout intent, `replace` the URL to strip the
   query (banner/poll key off the pending flag, not the query string). Poll
   `GET /api/billing/ledger` immediately, then every **2s**, until a TopUp fingerprint appears that
   was **not** in the baseline, or **30s** elapses.
3. **Completion gate:** a **new TopUp ledger row** only. On success, invalidate/refetch balance for
   display — balance delta is never the predicate (concurrent spend / pre-existing credits make it
   ambiguous).
4. **Missing baseline** (storage cleared, new tab, deep link): do **not** invent success via a
   time-window heuristic. Fall through to the same non-failure give-up path.
5. **On `/account?checkout=cancel`:** quiet dismissible notice; `replace` strip param; clear the
   checkout baseline. No polling.

### Decisions

| # | Decision | Reasoning |
|---|---|---|
| 1 | Completion signal = **new `TopUp` row**; balance is display-only after that | Webhook atomically credits balance and appends TopUp (`IStripeCreditStore`). Balance alone lies under concurrent spend or pre-existing credits — the false-failure that drives double purchase |
| 2 | Baseline at **checkout start** (`sessionStorage`), not only on success landing | If the webhook already won before first paint, a landing-only baseline freezes “no change” and polls to give-up despite credits having landed |
| 3 | Fingerprint = `(TimestampUtc, AmountUsd)` for `TopUp` rows | `LedgerEntryResponse` has no `Id`; pair is enough to detect a row outside the pre-checkout set within the recent ledger window |
| 4 | Missing baseline → **give-up path**, never a time heuristic | Heuristic windows fight clock skew, double-buy, and slow webhooks; false “success” is worse than a soft “may take a moment” |
| 5 | Poll **immediate + every 2s for 30s**; stop on first new TopUp or deadline | Covers normal redirect-beats-webhook lag; no backoff until measured p99 says otherwise. Permanent webhook failure stays map fog |
| 6 | During poll: **inline Account banner** “Applying your credits…” (spinner optional) | Constraint 4 wants processing on the account page, not a dead-end result route; does not block the rest of Account |
| 7 | On TopUp seen: brief **“Credits added”**; balance + ledger update; flash can auto-dismiss | Confirms completion without reintroducing `BillingResultPage` |
| 8 | On give-up: **“Payment received — credits may take a moment to appear. Refresh this page if the balance doesn’t update.”** — must not imply payment failed | Stripe already charged; copy discourages panic rebuy and offers light self-serve. Permanent-fail UX is still **Not yet specified** |
| 9 | `?checkout=success` **stripped with `replace` as soon as intent is accepted**; poll/banner key off pending flag | Refresh must not replay the success flow; pending flag cleared when TopUp is seen or give-up is dismissed |
| 10 | `?checkout=cancel` = quiet **“Checkout canceled — no charge was made.”**; strip param; **clear baseline** | Same URL hygiene as success; matches spirit of old cancel stub without a dedicated page |
| 11 | Re-checkout while poll runs: **allowed with confirm** (“A purchase is still applying — continue?”); on confirm, new checkout **overwrites** baseline/pending | Soft guard against accidental double-buy. Hard-disabling Buy recreates stuck-and-rebuy pressure — the failure mode this ticket exists to prevent. First new TopUp may end the banner even if a second purchase is still in flight; normal ledger/balance queries catch up |

### Consequences for the map

- Implementers need a **buy-path** touch (baseline snapshot on checkout success) and an
  **account-page** post-checkout controller (URL strip, banner, poll, give-up) — not account-only.
- Ledger query is already on the page (constraint 2); completion is a refetch policy + fingerprint
  compare, not a new backend Module (UL).
- `BillingResultPage` deletion (constraint 4) stands; this ticket supplies the behavior that replaces
  its success/cancel stubs.
- Does not graduate permanent-failed-webhook fog; give-up copy is intentionally temporary-safe.
- Does not block or unblock other open tickets.
