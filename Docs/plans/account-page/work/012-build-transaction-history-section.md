---
id: 012
title: Build the transaction history section
status: todo
implements: [005, 008]
depends_on: [007, 008]
---

## Goal

Render the ledger as one filterable list that tells a free-tier user the truth about what they were
charged.

## Constraints

- One list with All / Purchases / Usage filter chips defaulting to All, filtered client-side over
  the single existing ledger query — map constraint 9
- Chips map 1:1 to `LedgerEntryType`; a Free row **is** a Usage row —
  [Decide free-covered ledger row semantics](../tickets/008-decide-free-covered-ledger-row-semantics.md) #9
- A fully free-covered row renders **"Free"** in the amount slot, no currency, driven by the
  server's `isFreeCovered` — never by `amountUsd === 0` — #4, #8
- A partially covered row renders as an ordinary paid row: charged amount only, no marker — #5
- Spend rows format 4dp, TopUp rows 2dp — #6
- Row is a single line at `≥sm` — date · feature label · right-aligned `tabular-nums` amount —
  stacking to two lines below —
  [Choose the account page layout](../tickets/005-choose-account-page-layout.md) #9
- `Feature` renders through a label map (`Tutoring:Guidance` → `Paired Programmer · Guidance`) with a
  raw-string fallback, so a new `Feature` value never renders blank — #9
- Zero rows render uniformly: empty list with the chips still present, no new-user branch — #7
- Reads the shared `['billing','ledger']` hook; pagination is out of scope for this page

## Acceptance criteria

- The section renders rows with date, feature label, and amount, filtered by the three chips with
  All selected on load.
- A test proves an `isFreeCovered` row renders "Free" and appears under both All and Usage.
- A test proves a `$0.0042` spend renders at 4dp and a `$10` top-up renders at 2dp.
- A test proves an unmapped `Feature` value falls back to its raw string rather than rendering blank.
- A test proves the empty ledger renders the chips and an empty state, not an error.
- `npm test` passes.
