---
id: 006
title: Decide top-up completion signal and polling bounds
type: grilling
status: open
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

<!-- Empty until resolved. -->
