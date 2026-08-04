---
id: 011
title: Build the free-quota card and extract the UsageBar primitive
status: todo
implements: [001, 005]
depends_on: [007, 008]
---

## Goal

Build the free half of the wallet row and extract the fill math out of `TokenUsageBar` so both the
context-window strip and the account card share one primitive without sharing chrome.

## Constraints

- Extract a `UsageBar` primitive holding the fill math — pct clamp, the 0.3% min-fill invariant,
  width style. `TokenUsageBar` keeps its tooltip and footer chrome and consumes it —
  [Choose the account page layout](../tickets/005-choose-account-page-layout.md) #4
- The free-quota card consumes `UsageBar` directly with **visible** numbers and a flat accent fill;
  the red-at-80% ramp does not apply — spending a grant is the expected lifecycle, not an error —
  #4 and [Decide free-window expiry presentation](../tickets/001-decide-free-window-expiry-presentation.md) #6
- Active grant renders `used / max` in tokens. No token → USD conversion — 001 #3, map constraint 6
- A user with no `CreditBalance` row renders identically with the bar at 0% — 001 #4
- An exhausted grant collapses **permanently** to one muted line (`Free tokens — 20,000 of 20,000
  used`) rendered as page text with no card chrome, and the credits card goes full width — 001 #5,
  005 #5
- The account grant is the headline number. A notice appears **only** when per-IP headroom is the
  binding constraint, driven by `ipConstraint` — 001 #7
- No timestamps, no countdown, no expiry copy anywhere — 001 #1
- Free remaining is clamped at `≥ 0`

## Acceptance criteria

- All 11 existing `TokenUsageBar` tests pass unchanged; its rendered output including the color ramp
  and hover-only numbers is unaffected by the extraction.
- `UsageBar` has its own tests for the pct clamp and the 0.3% min-fill invariant.
- A test proves the free card renders visible `used / max` token counts and a flat fill at 85% usage
  — no red.
- A test proves the exhausted state renders the muted line with no card chrome and drops the wallet
  row to a single full-width credits card.
- A test proves the IP notice renders for `"Limited"` and `"Exhausted"` and is absent for `"None"`,
  and that no per-IP number is displayed.
- A zero-usage account renders the bar at 0% with no special-case copy.
- `npm test` passes.
