---
id: 001
title: Decide free-window expiry presentation
type: grilling
status: closed
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

**The window is gone.** The 48-hour expiry is eliminated as a product mechanic. Free tokens
become a one-time per-account grant with no expiry and no reset: a user gets their tokens and
spends them whenever they like. This was a deliberate product call, not a consequence of the
presentation questions below — it dissolved most of them.

### Decisions

| # | Decision | Reasoning |
|---|---|---|
| 1 | No time window. One-time per-account grant | Product direction. A grant that cannot lapse needs no countdown, no expiry copy, and no timestamps on the wire |
| 2 | Free tokens and paid credits render as **two distinct cards**, each owning its own query | The two are denominated in different units — tokens vs USD — and constraint 6 forbids converting between them, so a single merged balance is not honestly renderable. Separate cards match constraint 2's per-concern cache policy |
| 3 | Active grant renders as `TokenUsageBar` showing `used / max` in tokens | Constraint 6, minus its expiry clause |
| 4 | A user with no `CreditBalance` row renders identically, bar at 0% | With no clock there is no urgency to communicate; a full grant at 0% is self-explanatory. Requires the quota endpoint to synthesize zeros for a missing row rather than 404 |
| 5 | An exhausted grant collapses permanently to one muted line (`Free tokens — 20,000 of 20,000 used`) | The grant never resets, so this state is permanent. Past exhaustion the fact is worth a line, not a card. Keeps the buy CTA in exactly one place — the credits card |
| 6 | Override `TokenUsageBar`'s red-at-80% ramp for this instance | The ramp is correct for context-window fill, where filling up *is* a problem. Spending a free grant is the expected lifecycle; a permanent red bar states an error that isn't one |
| 7 | Show the account grant as the headline number; add a notice **only** when per-IP headroom is the binding constraint | Displaying a number the user cannot spend is the same transparency failure as the ledger `CostUsd` divergence. Folding IP headroom into the headline instead would make the balance move for reasons the user cannot see |

### Codebase facts that shaped this

- **The clock never started at sign-in.** The `CreditBalance` row is created lazily — only
  inside Reserve/Settle (`UsageEnforcer.cs:70`, `:169`) or a Stripe top-up
  (`EfStripeCreditStore.cs:38`). `FirstSeenUtc` was therefore the first *billable action*, not
  first sighting, and `CreditBalance.cs:9` describes it wrongly. A side effect: topping up
  before ever chatting started the trial clock at purchase time.
- **`IpFreeTokenCap = 60_000`** (`UsageEnforcer.cs:33`) is a second, invisible free-token limit.
  `ComputeFreeCover` (`:279-284`) takes `Math.Min(objectFreeRem, ipRem, …)`, so IP headroom can
  bind before the account grant does. `IpFreeUsage` never decays (`EfUsageStore.cs:63` only ever
  increments), so a shared NAT permanently exhausts after roughly three users' worth — and every
  legitimate user behind it afterward gets zero free tokens. Decision 7 exists because of this.
- **No credits or quota UI exists anywhere today.** `/api/billing/balance` and
  `/api/billing/ledger` ship with zero frontend consumers; `BillingResultPage.tsx` is a static
  stub with no data fetch. `TokenUsageBar` is used only for context-window fill
  (`ChatPanel.tsx:64-71`, `PromptLabWindow.tsx:212`) — same shape, unrelated concept.

### Consequences for the map

- **Unblocks [Define the free-quota endpoint contract](002-define-free-quota-endpoint-contract.md).**
  The endpoint needs `freeTokensUsed`, `freeQuotaMax`, and enough IP-headroom signal for
  decision 7. **No timestamps** — which is the question this ticket existed to settle.
- Constraint 6 in `map.md` loses its "with a window expiry" clause.
- Three new tickets: [Design the nav-dropdown balance summary](007-design-nav-dropdown-balance-summary.md),
  [Decide free-covered ledger row semantics](008-decide-free-covered-ledger-row-semantics.md),
  and [Remove the free-token time window from enforcement](009-remove-free-token-time-window.md).
- The `FreeMonthlyTokenQuota` naming fog resolves into ticket 009 — both *monthly* and *48h*
  are now wrong descriptions of the same field.
