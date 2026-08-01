---
id: 009
title: Remove the free-token time window from enforcement
type: grilling
status: open
blocked_by: []
---

## Question

[Decide free-window expiry presentation](001-decide-free-window-expiry-presentation.md) eliminated
the 48-hour free window as a product mechanic: free tokens are now a one-time per-account grant
with no expiry and no reset. That decision was made on the presentation side and has not yet been
carried into enforcement, where the window still exists and still cuts users off.

The mechanic lives in three places:

- `UsageEnforcer.WindowActive` (`:262-263`) — the hardcoded `48`, not in `UsageOptions`
- Its two call sites, `:72` (reserve) and `:176` (settle), which zero out free headroom when the
  window has lapsed
- `UsageEnforcerTests`, which exercises lapsed-window behavior directly

Resolve:

- **Retroactive grant.** Removing the gate means users whose window already lapsed with tokens
  unspent immediately regain them. At the current payer count this is near-zero users, but it is a
  real grant of value and should be a conscious call, not a side effect. Accept it, or backfill
  `FreeTokensUsedInWindow = FreeQuotaMax` for lapsed rows at migration time?
- **Naming.** `UsageOptions.FreeMonthlyTokenQuota` (`:8`) says *monthly*; `CreditBalance.cs:9`
  documents a one-time 48-hour window. Both are now wrong. What is the field called, and does the
  rename ride here or separately? This was fog on the map and graduates into this ticket.
- **`FirstSeenUtc` disposition.** It stops being load-bearing. Keep as an audit field, or drop it?
  Note it is also mildly wrong today: the row is created lazily on first *billable action*
  (`UsageEnforcer.cs:70`, `EfStripeCreditStore.cs:38`), so it records first spend or first top-up,
  never first sighting.
- **`FreeQuotaMax` per-row snapshot.** It is copied onto the row at creation from
  `UsageOptions`, so raising the configured grant does not lift existing users. With no window to
  reset, that snapshot is now permanent per account. Intended?
- Does any of this need a migration, or is it code-only?

Not in question: `IpFreeTokenCap` stays. The window guarded against hoarding; the IP cap guards
against multi-accounting, and removing one does not weaken the other. Its never-decaying behavior
is separate — logged as fog on the map.

## Answer

<!-- Empty until resolved. -->
