---
id: 001
title: Remove the free-token time window from enforcement
status: todo
implements: [001, 009]
depends_on: []
---

## Goal

Delete the 48-hour free-token window from enforcement so runtime behavior matches the one-time
per-account grant the map settled, and rename every field and config key that still describes a
window or a monthly quota.

## Constraints

- Delete `UsageEnforcer.WindowActive` and both call sites; free headroom is always
  `FreeQuotaMax − FreeTokensUsed`, still min'd with the IP cap —
  [Remove the free-token time window from enforcement](../tickets/009-remove-free-token-time-window.md) #1, #7
- Accept the retroactive grant. No freeze backfill of lapsed rows — #1
- `FreeQuotaMax` stays a per-row snapshot taken at `CreateNew`; name unchanged — #2, #6
- Drop `CreditBalance.FirstSeenUtc` from entity and schema. `IpFreeUsage.FirstSeenUtc` stays — #3, #9
- Rename `UsageOptions.FreeMonthlyTokenQuota` → `FreeTokenQuota` (config key becomes
  `Usage:FreeTokenQuota`) and `CreditBalance.FreeTokensUsedInWindow` → `FreeTokensUsed`, property
  **and** column — #4, #5
- One EF migration, no data backfill — #10
- Rewrite free-path comments (decorator Fast-tier note, `IUsageEnforcer`, `CreditBalance` summary)
  to one-time-grant framing — #8
- Docs radius is the live product surface only: `context.md`, `README.md`, `USER_TESTING.md`,
  appsettings. Historical recaps and handoffs stay as history — #11
- Land this **before** [Correct free-covered ledger row semantics](002-correct-free-covered-ledger-row-semantics.md);
  both edit `SettleAsync` and separate commits keep the history bisectable — #12

## Acceptance criteria

- `WindowActive` and every reference to it are gone from `UsageEnforcer`; no wall-clock value gates
  free headroom in reserve or settle.
- `ReserveAsync_WindowExpired_ThrowsInsufficientQuota` is deleted; the remaining `UsageEnforcerTests`
  pass, including a test proving free headroom is granted regardless of row age.
- `dotnet build` is clean with zero references to `FreeMonthlyTokenQuota`, `FreeTokensUsedInWindow`,
  or `CreditBalance.FirstSeenUtc` anywhere in the solution.
- A single EF migration drops `CreditBalances.FirstSeenUtc` and renames the used column to
  `FreeTokensUsed`; it applies to an existing database without data loss on other columns.
- `appsettings*.json` uses `Usage:FreeTokenQuota`; the app starts and binds the value.
- `context.md`, `README.md`, and `USER_TESTING.md` contain no description of a 48-hour window or a
  monthly quota.
- `dotnet test` passes.
