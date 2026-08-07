---
id: 002
title: Correct free-covered ledger row semantics
status: done
implements: [008]
depends_on: [001]
---

## Goal

Make `UsageLedgerEntry.CostUsd` on a Spend row record the amount actually debited, add
`FreeTokensCovered` to keep the free portion auditable, and expose `isFreeCovered` on the ledger DTO
so the transaction list can render a covered call as "Free" without re-deriving a billing rule in
the client.

## Constraints

- `CostUsd` on a Spend row becomes the amount actually debited — `$0` when fully free-covered, the
  prorated amount when partial. Fix lands in `SettleAsync` —
  [Decide free-covered ledger row semantics](../tickets/008-decide-free-covered-ledger-row-semantics.md) #1
- New nullable `FreeTokensCovered` (`int?`) column on `UsageLedgerEntry`; null means "written before
  this column existed", matching the `ProviderCostUsd` convention — #2
- The debited decimal and the stored decimal are **one** computed local, so ledger sums reconcile
  against `PaidCreditsBalance` — #3
- `LedgerEntryResponse` gains `isFreeCovered: bool` and nothing else; no token counts — #8
- No backfill. Pre-fix rows keep their notional `CostUsd` with `FreeTokensCovered` null and age out
  of the recent-N window — #7
- Filter chips stay 1:1 with `LedgerEntryType`; a Free row is a Usage row — #9
- Doc drift to fix: `UsageLedgerEntry.cs:25`, `context.md:237`, `LedgerEntryResponse.cs:7-9`
- Lands after [Remove the free-token time window from enforcement](001-remove-free-token-window-from-enforcement.md),
  as a separate commit — #10

## Acceptance criteria

- `SettleAsync` computes the prorated charge once into a local, debits that value, and writes that
  same value to `UsageLedgerEntry.CostUsd`.
- A test proves a fully free-covered settle writes `CostUsd == 0` and `FreeTokensCovered == totalTokens`.
- A test proves a partially covered settle writes the prorated debit and the free token count, and
  that the sum of Spend `CostUsd` values equals the total decrease in `PaidCreditsBalance`.
- An EF migration adds the nullable `FreeTokensCovered` column with no data backfill.
- `GET /api/billing/ledger` returns `isFreeCovered` per row, derived server-side, `false` for
  pre-fix rows. `ProviderCostUsd` and `RowVersion` remain omitted.
- `context.md` and the two source comments describe `CostUsd` as the amount actually debited.
- `dotnet test` passes.
