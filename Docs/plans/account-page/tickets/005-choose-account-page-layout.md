---
id: 005
title: Choose the account page layout
type: grilling
status: closed
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

**A single scrolling column with one paired wallet row on top, and a shared `AccountSection`
wrapper that owns card chrome plus the loading/error branch.** The page carries six blocks:
identity header, credits, free quota, history, preferences, account.

The two questions that did real work were not on the list above. First, `AccountPage` is the
**first scrolling page in the app** — `Layout.tsx:45` is `<main className="flex-1 overflow-hidden">`
and every existing page is a fixed-height split pane — so the page owns its own scroller and the
`#credits` anchor is inert without JS. Second, `TokenUsageBar` turned out to be the wrong shape to
reuse literally, which amends settled constraint 6.

### Decisions

| # | Decision | Reasoning |
|---|----------|-----------|
| 1 | Single scrolling column at `max-w-4xl`, top to bottom: identity header → post-checkout banner slot (ticket 006) → wallet row → history → preferences → account. Credits and free quota sit side by side in one `grid sm:grid-cols-2` wallet row; everything else is full width | Puts the buy action above the fold for a 402 arrival without demoting history far. Answers "one section or two" structurally — two cards and two queries, read as one row. Narrow width is one class change, not a layout |
| 2 | Shared `AccountSection` wrapper taking `{ title, anchorId, isLoading, error, children }`, owning card chrome and the loading/error branch. Chrome always renders at stable min-height; only the body swaps | Four consumers, and the deletion test puts the same four-way branch back in four files — a real seam by the project's own rule. Depth sits in the state machine, not the border. Stable height is what stops the wallet row jumping as queries land at different times |
| 3 | `AccountSection` keeps a **single** query state, fed by the query that is the section's reason to exist. Credits passes balance; a `/api/billing/packs` 502 degrades the pack area alone, inline, with a retry | The credits card is the one section with two queries. Taking the card down on a Stripe outage would hide a balance the server already returned — losing a fact we have to report one we don't. Keeps the wrapper's interface narrow instead of widening it for one caller |
| 4 | Extract a `UsageBar` primitive holding the fill math — pct clamp, the 0.3% min-fill invariant, width style. `TokenUsageBar` keeps its tooltip and footer chrome and consumes it; the free-quota card consumes it directly with **visible** numbers and a flat accent fill | The reusable thing is the math, not the chrome. Amends constraint 6 — see below. All 11 existing `TokenUsageBar` tests pass unchanged because behavior is identical and only composition moves |
| 5 | When the grant is exhausted the wallet row collapses: credits goes full width, and the muted line renders beneath it as page text with no card chrome | Exhausted is permanent (ticket 001 #5), so "one full-width credits card + a muted line" is the layout most accounts live in long-term — the *two-card row is the transient state*. The permanent state gets the better layout. Cost accepted: a one-time reflow when the quota query resolves, since the 2-col row renders optimistically while loading |
| 6 | On mount, if `location.hash` matches a section's `anchorId`: `scrollIntoView({ block: "start" })` then a ~2s fading accent ring. Implemented once in `AccountSection` | `id="credits"` alone does nothing here — the anchor lives in a nested scroller and React Router never triggers a hash jump on client-side navigation. And since credits is top-left, the scroll is a visual no-op, so **the ring is the actual feedback**. Every future section gets it free; the same hook later serves `?checkout=success` (ticket 006) |
| 7 | Brand-new user renders **uniformly** — zeros, empty lists, filter chips present, no special-case branch | Least code and nothing to drift out of sync as sections are added. Accepted cost, chosen with eyes open: the most common first view is `$0.00` beside three price buttons |
| 8 | Split the bottom into two sections — `Preferences` (AI provider picker, captioned "Applies to this browser") and `Account` (sign out, account-closure contact) | Provider is *device* state (`localStorage`) and the rest are *account* actions; the caption discharges the map's own fog note instead of letting the page imply the setting is account-scoped. Sign-out duplicates the nav dropdown deliberately — constraint 1 keeps it one click away and account pages conventionally carry it. Future editor/theme settings get a home that isn't beside a destructive action |
| 9 | Ledger row is a single line at `≥sm` — date · feature label · right-aligned `tabular-nums` amount — stacking to two lines below. `Feature` renders through a label map (`Tutoring:Guidance` → `Paired Programmer · Guidance`) with raw-string fallback | `LedgerEntryResponse` carries only four fields, so no table is warranted and no horizontal scroller is needed. `sm:` is the page's only breakpoint besides the wallet row — the app is a Monaco split-pane tool, so phone fidelity is a courtesy, not a target. The fallback means a new `Feature` value never renders blank |
| 10 | Identity header is a plain non-card `h1` + muted label inside the scroller, scrolling away. No query, no `AccountSection` | The nav bar already shows the label persistently and sits *outside* the scroller (`Layout.tsx:12`), so pinning a second copy spends vertical space duplicating something already onscreen. It reads from MSAL, so it has no loading or error state to wrap |

### Derived, not decided

**The unauthenticated shell is page chrome plus one sign-in panel.** Constraint 8 left "renders its
shell" open, but every data source on this page is `[Authorize]` — `/api/billing/balance`,
`/api/billing/ledger`, `/api/usage/quota` (ticket 002), `/api/billing/packs` (ticket 003). Nothing
is renderable signed out, so the only alternative is six cards each saying "sign in to see this".

### Codebase facts that shaped this

- **Nothing scrolls today.** `Layout.tsx:45` is `<main className="flex-1 overflow-hidden">`; every
  page is a fixed-height split pane. `AccountPage` must own an `h-full overflow-y-auto` container,
  and it is the reason decision 6 exists.
- **`TokenUsageBar` is a panel-footer strip, not a card element.** Its chrome is
  `border-t border-gray-700 bg-gray-850` full-bleed (`:20`), its numbers are **hover-only** in a
  tooltip (`:25-33`), its prop is named `contextWindowSize` — which would mean passing
  `freeQuotaMax` into it — and its color ramp is hardcoded with three tests asserting the
  thresholds (`TokenUsageBar.test.tsx:51-73`). Constraint 6's "override the ramp for this instance"
  was written before these facts were on the table.
- **The ledger DTO is narrower than a transaction table.** `LedgerEntryResponse.cs` exposes exactly
  `Type`, `AmountUsd`, `Feature`, `TimestampUtc` — no provider, model, or token counts, all
  deliberately withheld. Three visible things per row, which is what makes decision 9 cheap.
- **Existing state idiom, matched not invented.** Loading is a plain `text-sm text-gray-400` line
  (`ChallengeSelector.tsx:19-25`), errors are `FailureNotice`. No skeleton components exist
  anywhere in the app. Card language is `rounded-xl border border-gray-700 bg-gray-900` with
  `grid-cols-1 sm:grid-cols-2` (`HomePage.tsx:49`).

### Consequences for the map

- **Constraint 6 amended** — free quota renders via the `UsageBar` primitive extracted from
  `TokenUsageBar`, not via `TokenUsageBar` itself.
- **Constraint 7 amended** — the anchor ids are not free. `AccountPage` owns the app's first
  scroll container, and hash arrival needs the `AccountSection` scroll/ring behavior of decision 6.
- **Reconciled with ticket 006** (closed in parallel with this one). 006 #6 fixes the
  applying-credits state as a **page-level banner** that "does not block the rest of Account", so
  it slots above the wallet row and below the identity header — not inside the credits card. Same
  slot carries 006 #7's "Credits added" flash, #8's give-up copy, and #10's cancel notice. It is
  transient and reserves no height; it pushes the wallet row down once on appearance. The hash-ring
  of decision 6 never competes with it: `?checkout=` arrives from the Stripe redirect and
  `#credits` from the paywall CTA, which are different entries.
- **Ticket 008 untouched.** The row layout tolerates `$0.00` spend rows without deciding whether
  they exist.
- No new tickets. Every branch this ticket opened closed inside it.
