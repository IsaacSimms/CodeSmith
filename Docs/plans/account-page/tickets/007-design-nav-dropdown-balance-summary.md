---
id: 007
title: Design the nav-dropdown balance summary
type: grilling
status: open
blocked_by: [002]
---

## Question

Settled constraint 1 puts a "balance summary" in the top-nav dropdown alongside "Account" and
"Sign out". Nothing about that summary is specified, and **no ticket owned it** — ticket 005
covers the account page body, not the nav.

There is no credits or quota UI anywhere in the app today: grepping `CodeSmith.Web/src` for
`balance` returns nothing, and `/api/billing/balance` and `/api/billing/ledger` ship with zero
frontend consumers. This is the app's first at-a-glance money surface.

Resolve:

- What does the summary show — paid credits only, free tokens only, or both? Ticket 001 settled
  that the two cannot merge into one number (tokens vs USD, no conversion per constraint 6), and
  a dropdown has far less room than a page section.
- What does it show once the free grant is exhausted? Ticket 001 chose a muted collapsed line on
  the page; the dropdown may not have room for even that.
- Which queries back it? **Cache-key sharing is no longer open for the quota query**:
  [Define the free-quota endpoint contract](002-define-free-quota-endpoint-contract.md) decision 8
  makes the quota read lock-free, so it reports in-flight reservation holds and self-corrects only
  when the SPA invalidates on turn completion. The dropdown is visible *during* a conversation, so
  it must share the invalidated key or it will sit on an inflated number mid-chat. What remains open
  is the balance query, where no such coupling exists.
- When does it fetch? The dropdown is closed most of the time. Fetch on mount of `Layout`, on
  dropdown open, or on hover?
- What renders while loading, and what renders on error? A money figure that flickers or shows a
  stale value is worse than one that shows a skeleton.
- Does it render at all for an unauthenticated user? `AuthControls.tsx:8` returns `null` when
  `isMsalConfigured()` is false, so in local dev the dropdown does not exist — see constraint 8.
- Is the summary itself a link to `/account#credits`, or does it sit above the existing
  "Account" item as passive text?

## Answer

<!-- Empty until resolved. -->
