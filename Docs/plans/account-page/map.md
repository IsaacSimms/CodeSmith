---
name: account-page
status: working
---

# Account Page

## Destination

A shipped account page carrying the full in-app billing surface — paid credit balance, free
quota, transaction history, and credit-pack purchase — that is also the new home for sign-out
and the AI provider setting, structured so future settings sections drop in without rework.

## Notes

### Domain

Three surfaces (Tutoring, Prompt Lab, System Lab) run over one metered LLM layer. Every LLM
call debits a free token quota first, then a prepaid USD credit balance. Billing *writes*
credits; usage enforcement *debits* them — see `CLAUDE.md`. That seam holds throughout this map:
billing never references `IUsageEnforcer`.

### Skills every session should consult

- `grill-me` for every `grilling` ticket. Never answer the user's side.
- `tdd` when a ticket's answer lands in code. Backend tests mirror the project under test in
  `CodeSmith.Tests/`; frontend tests are colocated `*.test.tsx` (Vitest + RTL).

### Settled constraints

Resolved during the breadth-first charting interview. These are **decided** — a session working
this map should treat them as given, not reopen them. They are recorded here rather than as
closed tickets because they were settled before any ticket existed.

| # | Decision |
|---|---|
| 1 | **Nav entry point.** The email label in the top nav opens a dropdown containing a balance summary, "Account", and "Sign out". Reuses the outside-click/Escape machinery already in `AuthControls.tsx:87-114`. Sign-out does not move to the page alone — it stays one click away. |
| 2 | **Data shape.** No aggregate endpoint. The page composes separate queries client-side via TanStack Query: existing `/api/billing/balance` and `/api/billing/ledger`, plus a new `GET /api/usage/quota` that lives in the **enforcement** module, not billing. Each concern keeps its own cache and refetch policy. |
| 3 | **Pack catalog.** New `GET /api/billing/packs` reads the Stripe Price objects for the allow-listed ids and returns amount/currency/name, cached briefly in memory. Stripe stays the single source of truth for price so the displayed amount can never drift from the charged amount. `StripeOptions.PriceIds` remains the gate on what is purchasable. |
| 4 | **Post-checkout landing.** `StripeOptions.SuccessUrl`/`CancelUrl` point at `/account?checkout=success|cancel`. `BillingResultPage.tsx` is **deleted** — it exists only because there was no account page (its own header says `Inc 1 — no account UI`). The page shows a "top-up processing" state and polls within bounds, because the Stripe redirect normally beats the webhook. |
| 5 | **Provider picker relocates.** It is removed from `HomePage.tsx` entirely (including the display-order assertion at `HomePage.test.tsx:83-86`) and lives only on the account page. Deliberate decluttering of the landing surface. |
| 6 | **Free quota presentation.** Free tokens are a **one-time per-account grant with no expiry and no reset** — the 48h window is eliminated as a product mechanic (see [Decide free-window expiry presentation](tickets/001-decide-free-window-expiry-presentation.md)). Shown in **tokens** via a `UsageBar` primitive extracted from `TokenUsageBar.tsx` — *amended by [ticket 005](tickets/005-choose-account-page-layout.md)*: `TokenUsageBar` itself is a panel-footer strip with hover-only numbers and a `contextWindowSize` prop, so the reusable part is the fill math, not the chrome. The account instance renders visible numbers and a flat fill, dropping the red-at-80% ramp. No token→USD conversion — an estimate rendered as currency becomes a number users hold you to. |
| 7 | **Page structure.** Plain composition: `AccountPage` renders section components, each owning its own queries, with anchor ids (`#credits`) for deep links. No section registry, no nested routes — one consumer is a hypothetical seam, and the deletion test collapses a registry into two JSX lines. Convert to nested routes later if the section count justifies it. *Amended by [ticket 005](tickets/005-choose-account-page-layout.md)*: the sections share an `AccountSection` wrapper (chrome + `anchorId` + the loading/error branch), and the anchor ids are **not free** — `Layout.tsx:45` is `overflow-hidden`, so `AccountPage` owns the app's first scroll container and hash arrival needs explicit `scrollIntoView` + highlight. |
| 8 | **Unauthenticated access.** `/account` renders its shell with a sign-in prompt rather than redirecting, so a bookmarked URL is not silently discarded. Plus a dev-only nav entry gated on the existing `isMsalConfigured()` check, because `AuthControls.tsx:8` returns `null` in local dev and would otherwise leave the page unreachable. |
| 9 | **Transaction history.** One list with filter chips — All / Purchases / Usage — defaulting to All. Client-side filtering over the single existing ledger query. `Feature` gives usage rows meaning. |
| 10 | **Paywall routing.** `ClientFailure` gains an optional `action: { label, href }`. `interpretError` populates it for `paywall` (→ `/account#credits`) and `login`; `FailureNotice` renders it when present and otherwise stays presentational. Two real consumers, so a real seam. Reverses the deliberate "no CTA" note at `FailureNotice.tsx:9`, which was written when there was nowhere to send the user. |
| 11 | **Account deletion.** A documented request path (support contact) only. See Out of scope. |
| 12 | **Provider default correctness.** Provider preference lifts into a context fetched once at `Layout`, following the `NavigationContext` pattern. This fixes a live defect: `HomePage.tsx:18` is the only caller passing `serverDefault`, while `ChatWindow.tsx:39`, `PromptLabWindow.tsx:35`, and `SystemLabWindow.tsx:29` call the hook bare and resolve to the hardcoded `"Anthropic"` at `useProviderPreference.ts:19` — so the UI can show xAI while the request sends Anthropic. |

### Standing preferences

- Edit and refactor existing code before creating new files. Two deletions are already
  mandated by the constraints above (`BillingResultPage.tsx`, the HomePage provider picker).
- Explain what changed, why, and the codebase impact when overwriting a file.
- TDD where practical.

## Frontier

<!-- DERIVED. Regenerated on every close. Do not edit by hand. -->

- [Design the nav-dropdown balance summary](tickets/007-design-nav-dropdown-balance-summary.md) — grilling
- [Decide free-covered ledger row semantics](tickets/008-decide-free-covered-ledger-row-semantics.md) — grilling
- [Remove the free-token time window from enforcement](tickets/009-remove-free-token-time-window.md) — grilling
- [Resolve the request provider from AiOptions.ActiveProvider](tickets/010-resolve-request-provider-from-active-provider.md) — grilling

## Decisions so far

- [Decide free-window expiry presentation](tickets/001-decide-free-window-expiry-presentation.md) —
  the 48h window is **eliminated**: free tokens are a one-time per-account grant, no expiry, no
  reset. Free and paid render as two distinct cards; an exhausted grant collapses permanently to a
  muted line; the account grant is the headline number, with a notice only when per-IP headroom
  binds. The quota endpoint therefore ships **no timestamps**.
- [Define the free-quota endpoint contract](tickets/002-define-free-quota-endpoint-contract.md) —
  `GET /api/usage/quota` `[Authorize]` on a new `UsageController`, returning
  `{ freeTokensUsed, freeQuotaMax, ipConstraint }`. Backed by a new `GetQuotaAsync` read on
  `IUsageEnforcer` so the remaining-quota rule is never re-derived outside the module that enforces
  it. IP headroom crosses as a three-state enum, never a number — the endpoint is pollable and a raw
  value would meter co-tenants on a shared NAT. The read is lock-free and never creates a row.
- [Define the pack-catalog endpoint contract](tickets/003-define-pack-catalog-endpoint-contract.md) —
  `GET /api/billing/packs` `[Authorize]`; Product name + decimal major USD amount + currency;
  order follows `PriceIds`; 5 min in-memory success cache (load only); Stripe down → 502;
  unusable allow-list ids skipped (all-bad → `200 []`).
- [Design the provider-preference context](tickets/004-design-provider-preference-context.md) —
  new `ProviderPreferenceContext` at `Layout` exposing
  `{ provider, setProvider, availableProviders, isReady }`, with `useProviderPreference` surviving
  as its internal storage adapter. `isReady = hasStoredChoice || query.isSuccess` gates the Start
  control on all three surfaces, so no request ever carries a provisional provider — but the gate is
  labeled and bounded at ~3s, after which Start omits `provider` and lets the server decide. Display
  order and labels move to the account page picker; the `localStorage` key is unchanged and
  self-heals invalid values. Uncovered a server-side twin of the same defect — see ticket 010.
- [Decide top-up completion signal and polling bounds](tickets/006-decide-topup-completion-signal.md) —
  completion = new ledger `TopUp` (not balance delta); baseline fingerprints at checkout start;
  poll ledger every 2s for 30s; inline “applying credits” banner; give-up never says payment
  failed; strip `?checkout=success|cancel` with replace; cancel is a quiet notice; re-buy during
  poll allowed with confirm.
- [Choose the account page layout](tickets/005-choose-account-page-layout.md) —
  single scrolling column at `max-w-4xl`; credits + free quota paired in a wallet row above the
  fold, collapsing to a full-width credits card plus a muted line once the grant is spent. A shared
  `AccountSection` wrapper owns card chrome, `anchorId`, and the loading/error branch at stable
  height, so sections resolving at different times never shift the layout; it takes **one** query
  state, so a packs 502 degrades inline rather than hiding a balance we already have. Hash arrival
  is `scrollIntoView` + a fading ring, because `AccountPage` is the app's first scroll container and
  a bare anchor id is inert. Zero states render uniformly with no new-user branch. Bottom splits
  into `Preferences` (provider, "applies to this browser") and `Account` (sign out, closure
  contact). Ledger rows are single-line ≥`sm` with a `Feature` label map. Amends constraints 6
  and 7; 006's applying-credits banner is page-level, above the wallet row.

## Not yet specified

<!-- Fog: in-scope questions not yet sharp enough to ticket. -->

- **Ledger pagination.** `GetRecentAsync` has no cursor or offset and `take` is clamped to 100.
  The right pagination shape is easier to choose once real row volumes exist.
- **Permanently-failed webhook.** What the user sees when a top-up never lands — distinct from
  the ordinary redirect-beats-webhook lag covered by ticket 006.
- **Config-driven pack ordering and badges.** "Most popular" and non-price-derived ordering
  would need config metadata alongside the Stripe-sourced amount.
- **Never-decaying per-IP free cap.** `IpFreeTokenCap = 60_000` (`UsageEnforcer.cs:33`) is an
  aggregate across all objectIds on an IP, and `IpFreeUsage` only ever increments
  (`EfUsageStore.cs:63`). A shared NAT — office, campus, household — permanently exhausts after
  roughly three users' worth, and every legitimate user behind it afterward gets zero free tokens
  forever. The cap itself stays (it is the multi-account defense); whether it should decay, and on
  what basis, is enforcement work outside this page's presentation scope.
- **Aggregate spend rollups.** Spend by `Feature` / `Provider` over time. The data supports it —
  `UsageLedgerEntry` persists `Feature`, `Provider`, `Model`, `TimestampUtc` — but it needs
  aggregation `IUsageLedgerRepository` does not have, and it collides with the unresolved
  pagination question above. Constraint 9's per-row list covers "where did my money go" at the
  resolution needed to ship.
- **Provider as account state rather than device state.** `useProviderPreference` is
  `localStorage`-backed and per-device. Server-persisting it is new settings architecture,
  deliberately excluded from this destination. Ticket 005 #8 removes the *misleading* half of this
  — the picker is captioned "Applies to this browser" and sits in `Preferences`, separate from the
  `Account` section — so what remains is only the product question of whether it should follow the
  user across devices.

## Out of scope

<!-- Ruled beyond the destination. Closed, never graduates. -->

- **Stripe Customer identity.** Checkout keeps metadata-only identity (`StripeBillingService.cs:73`).
  No `StripeCustomerId`, no Customer creation. Ruled out explicitly: the one-to-one Customer↔objectId
  mapping is a permanent invariant, and it is not worth committing to at the current (near-zero)
  payer count. Revisit only if real usage materializes.
- **Stripe Customer Portal.** Depends on Customer identity above. Receipts are covered by Stripe's
  automatic payment receipt emails (a Dashboard setting, no code).
- **Auto-recharge and subscription plans.** Off-session charging and dunning are their own
  subsystem; subscriptions would mean two billing models coexisting behind every quota check.
- **Account deletion and data retention policy.** Self-serve deletion collides with credit-balance
  disposition, financial-record retention obligations on `UsageLedgerEntry`, and Entra directory
  deletion via Graph. Its own effort. This map ships a documented support-contact path only.
- **New non-billing settings** — editor settings, theme, keybindings. The page is structured to
  accept them; none are built here.
