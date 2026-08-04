---
id: 007
title: Build the account page shell and AccountSection wrapper
status: todo
implements: [005]
depends_on: []
---

## Goal

Stand up `/account` — route, scroll container, identity header, the shared `AccountSection` wrapper
with its loading/error branch and hash-arrival behavior, the unauthenticated shell, and the money and
token formatters every later section reuses.

## Constraints

- Single scrolling column at `max-w-4xl`, top to bottom: identity header → post-checkout banner slot
  → wallet row → history → preferences → account —
  [Choose the account page layout](../tickets/005-choose-account-page-layout.md) #1
- `AccountPage` owns an `h-full overflow-y-auto` container; `Layout.tsx:45` is `overflow-hidden` and
  every other page is a fixed-height split pane — Codebase facts
- Shared `AccountSection` taking `{ title, anchorId, isLoading, error, children }`, owning card
  chrome and the loading/error branch, rendering at stable min-height so sections resolving at
  different times never shift the layout — #2
- `AccountSection` keeps a **single** query state; do not widen it for a multi-query caller — #3
- On mount, if `location.hash` matches a section's `anchorId`: `scrollIntoView({ block: "start" })`
  then a ~2s fading accent ring, implemented once in `AccountSection` — #6
- Identity header is a plain non-card `h1` + muted label inside the scroller, no query, no
  `AccountSection` — #10
- Unauthenticated `/account` renders page chrome plus **one** sign-in panel — it never redirects, and
  it is not six cards each saying "sign in" — constraint 8 / Derived
- A dev-only nav entry gated on the existing `isMsalConfigured()` check, since `AuthControls.tsx:8`
  returns `null` in local dev and would otherwise leave the page unreachable — constraint 8
- Match the existing state idiom: loading is a plain `text-sm text-gray-400` line, errors are
  `FailureNotice`, cards are `rounded-xl border border-gray-700 bg-gray-900`. No skeletons — Codebase facts
- Formatters: USD renders 2dp, but a spendable sub-cent balance renders `< $0.01`, never `$0.00`;
  `$0.00` is reserved for a true zero. Ledger Spend rows format 4dp, TopUp rows 2dp —
  [Decide free-covered ledger row semantics](../tickets/008-decide-free-covered-ledger-row-semantics.md) #6, #11

## Acceptance criteria

- `/account` is routed and renders the identity header from `resolveAccountLabel`, scrolling inside
  its own container while the nav bar stays fixed.
- `AccountSection` renders chrome at stable height in all four states (loading, error, empty, loaded);
  a test proves the section's height does not change between loading and loaded.
- A test proves arriving at `/account#credits` scrolls the matching section into view and applies
  the transient ring, and that a non-matching hash is inert.
- Unauthenticated `/account` renders the shell plus a single sign-in panel and no redirect occurs.
- With MSAL unconfigured, a nav entry to `/account` is present; with MSAL configured, it is not.
- Formatter tests cover: `$0.00` for a true zero, `< $0.01` for a spendable sub-cent balance, 4dp
  Spend rows, 2dp TopUp rows, and thousands-separated token counts.
- `npm test` passes.
