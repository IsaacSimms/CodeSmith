# Google IdP Sign-In — Design Grill

**Date:** 2026-07-19  
**Type:** ideation + implementation  
**Environment / Systems:** Entra External ID (CIAM), MSAL SPA, Google Cloud OAuth (portal later)

## TL;DR

Locked and implemented a thin Google sign-in path: federate Google into existing CIAM, SPA **Sign in** dropdown with **Continue with email** / **Continue with Google**, no account linking, no API auth changes. SPA + tests + `context.md` on `master`; portal work deferred to the user handoff runbook §8.

## Context & Goal

Users could only complete auth via the current Entra/MSAL path (local CIAM email). Goal: offer Google as a second sign-in method after **Sign in**, without a second identity stack. Increment 1 had deferred social IdP to Inc 2.

## Key Points Explored

- **Where Google lives:** Federate into CIAM vs dual Google+MSAL stacks vs broker migration. Federation keeps one Entra JWT and one `ObjectId` for usage/billing.
- **Chooser ownership:** App dropdown vs Entra-hosted multi-IdP page vs hybrid double-chooser.
- **Account linking:** Same email across IdPs → two CIAM users / balances if unlinked. Full linking is a large Entra/Graph lift; deferred entirely.
- **Non-Google label:** Current path is CIAM local email, not Microsoft account SSO — label **Continue with email**, not Microsoft.
- **IdP targeting:** Google uses MSAL `extraQueryParameters: { domain_hint: "google" }`; email path soft (stock local CIAM), no hard local-only lock in v1.
- **Self-service:** First Google login creates a CIAM user and free-tier `ObjectId` (open signup).
- **Portal vs code:** User owns Google Cloud OAuth client + Entra IdP + user flow; agent owns SPA/docs/tests. API/`ICurrentUser` unchanged.
- **Tests:** Unit/component only (mock MSAL); no live Google in CI.
- **OAuth consent:** In production when wired (personal project; no external users yet).
- **Ship without portal:** Always show both buttons; Google may fail until handoff §8 is done.
- **Git:** Implement on `master` (token streaming already merged; default branch is `master`, not `main`).

## Decisions & Outcomes

| Decision | Choice |
|----------|--------|
| Architecture | Google federated into Entra External ID only |
| UX | Sign in → dropdown → Continue with email \| Continue with Google |
| MSAL Google | `domain_hint=google` |
| Linking | Out of scope for this lift |
| Self-signup via Google | Open |
| API / billing seams | No change |
| Tests | Vitest + RTL, mock MSAL |
| Google consent | Production when configured |
| Feature flag | None — always show both |
| Split-account copy | Quiet line under dropdown: “Use the same sign-in method next time.” (linking may never ship) |
| Button chrome | Text only (Q15); official Google button asset deferred to a later iteration |
| Branch | `master` |
| External work companion | `Docs/Handoffs.User/2026-07-19-google-idp-sign-in-handoff.md` §8 |
| In-repo docs | This recap + `context.md` auth section on implement; handoff already holds external steps |

## Open Questions / Next Steps

- **User (later):** run handoff §8 (Google Cloud + Entra) when ready; not required for SPA code complete.
- Optional later: hard email-only hosted page, account linking (explicitly may never happen), real Microsoft account federation, official Google button branding (C).

## Artifacts

| Artifact | Location | State |
|----------|----------|-------|
| User + design handoff / external runbook | `Docs/Handoffs.User/2026-07-19-google-idp-sign-in-handoff.md` | Written; §8 is portal checklist |
| This recap | `Docs/Recaps/2026-07-19-google-idp-sign-in-design.md` | Written |
| Auth UI | `CodeSmith.Web/src/auth/AuthControls.tsx` | Sign in dropdown + helper |
| MSAL config | `CodeSmith.Web/src/auth/msalConfig.ts` | `buildLoginRequest` + `buildGoogleLoginRequest` |
| Auth tests | `CodeSmith.Web/src/auth/*.test.ts(x)` | Unit coverage for requests + chooser |
| Entra setup guide | `Docs/general/entra-external-id-azure-setup.md` | Unchanged; handoff is federation source of truth |
| `context.md` | repo root | Auth section updated for SPA chooser |
