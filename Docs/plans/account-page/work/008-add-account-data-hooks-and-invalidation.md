---
id: 008
title: Add account data hooks, prefetch, and turn-settle invalidation
status: done
implements: [002, 003, 007, 008]
depends_on: [002, 003, 004]
---

## Goal

Give the nav dropdown and every account section one shared cache: typed hooks over the four account
queries, prefetch at the authenticated shell, and invalidation on metered turn settle and on top-up
detection, so no two surfaces can disagree about the same number.

## Constraints

- Shared TanStack keys, one cache for dropdown and page: `['usage','quota']`,
  `['billing','balance']`, `['billing','ledger']`, plus the packs query —
  [Design the nav-dropdown balance summary](../tickets/007-design-nav-dropdown-balance-summary.md) #7
- Prefetch quota and balance when authenticated, at `Layout` or the equivalent authenticated shell —
  not fetch-on-open — so the lock-free quota read's mid-chat overstatement self-corrects — #6
- Invalidate `['usage','quota']`, `['billing','balance']`, **and** `['billing','ledger']` on every
  metered turn settle and on top-up success. One client rule, no free/paid branching in the
  invalidator — #8, amended by
  [Decide free-covered ledger row semantics](../tickets/008-decide-free-covered-ledger-row-semantics.md) #12
- Packs is an **independent** query: section-level error on 502, empty-state on `[]`, no coupling to
  balance / ledger / quota failure —
  [Define the pack-catalog endpoint contract](../tickets/003-define-pack-catalog-endpoint-contract.md) Consequences
- Clamp free remaining at `≥ 0`; reservation holds can make `used` temporarily overshoot `max` —
  [Define the free-quota endpoint contract](../tickets/002-define-free-quota-endpoint-contract.md) #8
- Each concern keeps its own cache and refetch policy; there is no aggregate endpoint — map
  constraint 2
- All calls go through the existing `apiClient` (native `fetch`, relative `/api` paths) as TanStack
  Query hooks, not raw `useEffect` + `fetch`

## Acceptance criteria

- Typed hooks exist for quota, balance, ledger, and packs, each with the key above, and the response
  types match the shipped endpoints including `ipConstraint` and `isFreeCovered`.
- A test proves an authenticated mount prefetches quota and balance, and an unauthenticated mount
  prefetches neither.
- A test proves a metered turn settle on each of the three surfaces invalidates all three keys.
- A test proves free remaining never renders negative when `freeTokensUsed > freeQuotaMax`.
- A packs 502 leaves the balance, ledger, and quota queries in their success state.
- `npm test` passes.
