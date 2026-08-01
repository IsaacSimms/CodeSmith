---
id: 001
title: Decide free-window expiry presentation
type: grilling
status: open
blocked_by: []
---

## Question

Free quota is a one-time 48-hour window from `CreditBalance.FirstSeenUtc`, not a recurring
allowance, and it does not reset. Settled constraint 6 fixes the *unit* (tokens, via
`TokenUsageBar`) but not the lifecycle presentation.

Resolve:

- What does the section show **during** the window — remaining tokens, a countdown, both?
- What does it show **after** expiry — a spent-out bar, a collapsed line, or nothing at all?
- What does it show to a user who has spent **zero** tokens and may not know a trial is running?
- Is expiry framed as a trial ending (product framing) or a quota lapsing (mechanical framing)?
- Does a user with paid credits still see the free-window section once it has expired?

This ticket runs first because it determines which facts the quota endpoint must expose —
whether the server sends raw state (`firstSeenUtc`) or a computed `expiresAtUtc` / `isActive`.
Ticket 002 is blocked on the answer.

## Answer

<!-- Empty until resolved. -->
