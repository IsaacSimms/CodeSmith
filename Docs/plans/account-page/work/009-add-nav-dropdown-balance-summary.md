---
id: 009
title: Convert AuthControls to a dropdown with a balance summary
status: todo
implements: [007]
depends_on: [007, 008]
---

## Goal

Turn the authenticated nav from label + Sign out into the constraint-1 dropdown carrying a
lifecycle-switched balance summary, Account, and Sign out.

## Constraints

- Menu order: balance summary (passive text, not a link, not a menuitem action) → Account → Sign out.
  Reuse the outside-click / Escape machinery already at `AuthControls.tsx:87-114` —
  [Design the nav-dropdown balance summary](../tickets/007-design-nav-dropdown-balance-summary.md) #5, map constraint 1
- Lifecycle mode switch, never both units at once: free **remaining** only while
  `freeTokensUsed < freeQuotaMax`, formatted paid USD after that — #1, #2, #3
- Exhaustion is the **account grant** only; IP constraint never appears in the dropdown — #4
- Loading → muted placeholder in a stable slot; never invent `$0.00` or a token count — #9
- Error on the query backing the active mode → omit the summary row entirely; Account and Sign out
  still render — #10
- Paid mode shows formatted USD including `$0.00` for a true zero, and `< $0.01` for a spendable
  sub-cent balance — #11, amended by
  [Decide free-covered ledger row semantics](../tickets/008-decide-free-covered-ledger-row-semantics.md) #11
- Reads the shared hooks from
  [Add account data hooks](008-add-account-data-hooks-and-invalidation.md); it must not fetch on open
  or mount its own queries — #6, #7
- Unauthenticated users never see this summary; MSAL-off dev has no `AuthControls` at all

## Acceptance criteria

- The authenticated nav opens a dropdown that closes on outside click and on Escape, with the three
  rows in order.
- A test proves the free mode renders remaining tokens only, and that crossing
  `freeTokensUsed >= freeQuotaMax` switches the same slot to paid USD.
- A test proves loading renders the muted placeholder without layout shift, and that an error on the
  active mode's query hides only the summary row.
- A test proves the summary is not focusable or clickable as navigation, and that Account is the
  only path to `/account`.
- The dropdown and `/account` render the same balance from one cache — no second fetch.
- `npm test` passes.
