---
id: 009
title: Remove the free-token time window from enforcement
type: grilling
status: closed
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

**Enforcement matches ticket 001.** The 48-hour gate is deleted from `UsageEnforcer`; free
headroom is always `FreeQuotaMax − FreeTokensUsed` (still min'd with the IP cap). Live code,
schema, and product docs drop every name and comment that still describes a window or a monthly
quota. No freeze backfill — the system is not production and no one sits in a lapsed-with-remainder
state worth preserving.

### Decisions

| # | Decision | Reasoning |
|---|----------|-----------|
| 1 | **Accept the retroactive grant** — remove `WindowActive`; do not set used = max for "lapsed" rows | Freezing would preserve a product mechanic ticket 001 already rejected. Empty / non-prod environment makes the grant free. Conscious call, not an accident |
| 2 | **`FreeQuotaMax` stays a per-row snapshot taken at `CreateNew`** | One-time grant implies a fixed size per account. Raising `UsageOptions` lifts **new** rows only; bulk uplift of existing accounts is an explicit future migration if ever needed |
| 3 | **Drop `CreditBalance.FirstSeenUtc`** from entity and schema | No longer load-bearing. Was never true first-sighting (lazy create on first spend or top-up). Keeping it as audit was optional; clean model wins |
| 4 | **Rename `UsageOptions.FreeMonthlyTokenQuota` → `FreeTokenQuota`** | "Monthly" is false. Config key becomes `Usage:FreeTokenQuota`. Touch call sites (enforcer seed, Stripe top-up seed, tests, appsettings) |
| 5 | **Rename `CreditBalance.FreeTokensUsedInWindow` → `FreeTokensUsed`** (property **and** column) | Aligns with ticket 002's wire name `freeTokensUsed`. No "window" left in the model |
| 6 | **`FreeQuotaMax` name unchanged** | Already accurate; 002's contract already uses it |
| 7 | **Delete `WindowActive` and every call site / test that depends on it** | Reserve/settle free rem no longer gated on wall-clock. Delete `ReserveAsync_WindowExpired_ThrowsInsufficientQuota`. Rewrite log fields that say `WindowActive` |
| 8 | **Rewrite free-path comments** (decorator Fast-tier note, `IUsageEnforcer`, `CreditBalance` summary) to **one-time grant / while consuming free quota** — no 48h framing | Behavior of Fast-tier while on free tokens stays; only the window framing was wrong |
| 9 | **`IpFreeUsage.FirstSeenUtc` stays** | Not the objectId window; it is IP-row metadata for a cap that remains in force |
| 10 | **One EF migration in this ticket** — drop `CreditBalances.FirstSeenUtc`; rename used column → `FreeTokensUsed`; **no data backfill** | Forced by decisions 3 and 5. Clean schema, not property→legacy-column mapping |
| 11 | **Docs radius: live product surface only** | Code, tests, migration, `context.md`, `README.md`, `USER_TESTING.md`, appsettings. Historical recaps/handoffs stay as history |
| 12 | **Implement before [ticket 008](008-decide-free-covered-ledger-row-semantics.md)** | Unchanged from 008 #10: both edit `SettleAsync`; 009 lands first so 008 rebases onto a smaller enforcer. Separate commits for bisect |

### Codebase facts that shaped this

- **The gate is three lines of product law, not config.** `WindowActive` hardcodes `48` and is
  not on `UsageOptions` — removing it is a code delete, not a config flip.
- **`FirstSeenUtc` never meant first sighting.** Row creation is lazy inside Reserve/Settle and
  Stripe top-up (`UsageEnforcer.cs:70`, `EfStripeCreditStore.cs:38`), so the clock started on first
  billable action. Dropping the field also drops that lie.
- **IP cap is independent.** `IpFreeTokenCap = 60_000` and never-decaying `IpFreeUsage` stay; map
  fog already owns whether the cap should decay later.
- **008 already ordered itself after this ticket** for implementation. Closing 009 does not change
  that; it unblocks writing the enforcer cleanup.

### Consequences for the map

- **Carries [ticket 001](001-decide-free-window-expiry-presentation.md) into enforcement** so
  presentation, quota endpoint (002), and runtime agree.
- **Unblocks implementing [ticket 008](008-decide-free-covered-ledger-row-semantics.md)** in the
  order 008 already chose (009 first).
- **No new tickets.** Naming fog that lived on this ticket is resolved; never-decaying IP cap
  remains map fog, out of this ticket by design.
- **Doc drift to fix when the code lands:** `context.md`, `README.md`, `USER_TESTING.md` still
  describe the 48h window and `FreeMonthlyTokenQuota` / `FreeTokensUsedInWindow` / `FirstSeenUtc`
  on `CreditBalance`.
- **Config drift:** any `Usage:FreeMonthlyTokenQuota` in appsettings becomes
  `Usage:FreeTokenQuota`.
