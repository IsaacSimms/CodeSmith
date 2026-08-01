---
id: 005
title: Choose the account page layout
type: grilling
status: open
blocked_by: []
---

## Question

Settled constraint 7 fixes the *structure* (plain composition, sections own their queries, anchor
ids) but not the visual arrangement. The page carries five things at launch:

1. Identity header — the label from `resolveAccountLabel`
2. Credits — paid balance, purchasable packs, `#credits` anchor target for paywall links
3. Free quota — token bar plus window state (shape depends on ticket 001)
4. Transaction history — filterable list
5. Preferences — provider setting, sign-out, account-closure contact

Resolve:

- Arrangement: single scrolling column, two-column split, or card grid?
- What sits **above the fold**? A user arriving from a 402 wants the buy action immediately;
  a user arriving from the nav dropdown may want history. Those pull in different directions.
- Are credits and free quota one section or two? They are different units and different
  lifecycles, but a user reads them as one question: "what can I spend?"
- Where does sign-out sit, given the nav dropdown already carries it (constraint 1)?
- How does each section render **independently** in loading, empty, and error states, since each
  owns its own query and they will not resolve together?
- What does the page look like for a brand-new user — zero balance, zero ledger rows, active free
  window? That is the most common first view and the easiest one to leave looking broken.
- Responsive behavior at narrow widths.

## Answer

<!-- Empty until resolved. -->
