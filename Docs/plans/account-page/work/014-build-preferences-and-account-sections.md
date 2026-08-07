---
id: 014
title: Build the Preferences and Account sections and relocate the provider picker
status: done
implements: [004, 005]
depends_on: [006, 007]
---

## Goal

Close the bottom of the page: the AI provider picker in its new home, sign-out, and the
account-closure contact path — and strip the picker out of `HomePage`.

## Constraints

- Two sections, not one: `Preferences` (AI provider picker, captioned "Applies to this browser") and
  `Account` (sign out, account-closure contact) —
  [Choose the account page layout](../tickets/005-choose-account-page-layout.md) #8
- Display order and labels move from `HomePage.tsx:7-14` into the picker component, not the context —
  [Design the provider-preference context](../tickets/004-design-provider-preference-context.md) #8
- The picker is removed from `HomePage.tsx` entirely, including the display-order assertion at
  `HomePage.test.tsx:83-86` and the `useProviderPreference` mock at `HomePage.test.tsx:18` — map
  constraint 5, 004 Derived. **Sequencing note:** the map mandates both the removal and the
  relocation but not their order; they land in this one item so the app is never left with no picker
  at all.
- The picker reads and writes through `ProviderPreferenceContext` — one source, never a second
  independent fetch
- Sign-out duplicates the nav dropdown deliberately; account pages conventionally carry it — 005 #8
- Account closure is a documented support-contact path only; no self-serve deletion — map constraint
  11 / Out of scope
- Future editor and theme settings get a home in `Preferences` that is not beside a destructive
  action — 005 #8

## Acceptance criteria

- `/account` renders `Preferences` with the provider picker and the "Applies to this browser"
  caption, and a separate `Account` section with sign out and the closure contact.
- A test proves selecting a provider in the picker changes what the three surfaces send, through the
  shared context.
- `HomePage.tsx` contains no provider picker and no provider labels; `HomePage.test.tsx` no longer
  mocks `useProviderPreference` and its display-order assertion is gone.
- A test proves sign-out from the Account section performs the same MSAL logout as the nav dropdown.
- `npm test` passes.
