# Google IdP External Wiring + SWA domain_hint Fix

**Date:** 2026-07-27  
**Type:** ops  
**Environment / Systems:** Entra External ID (CIAM `codesmithapp`), Google Cloud OAuth, Azure Static Web App, MSAL SPA

## TL;DR

Completed the non-code Google federation runbook (Google Cloud OAuth client, Entra Google IdP, `SignUpOrSignIn` user flow) and fixed SWA Google sign-in by using `domain_hint: "Google"` (capital G). Hosted-page Google worked earlier; lowercase `domain_hint=google` failed on desktop with AADSTS500208 without ever reaching Google.

## Context & Goal

SPA already shipped Email / Google chooser + `buildGoogleLoginRequest`. Goal was **external wiring only** so Google sign-in works end-to-end, then unblock production SWA deploy when CI failed on an unrelated TypeScript unused-import in a test file.

Companion design/runbook: `Docs/Handoffs.User/2026-07-19-google-idp-sign-in-handoff.md` §8; design recap: `Docs/Recaps/2026-07-19-google-idp-sign-in-design.md`.

## Key Points Explored

- **Step 1 gather:** Tenant ID `25463a03-81a7-448c-9873-99d2ecc03eb8`, primary domain `codesmithapp.onmicrosoft.com`, CIAM host `https://codesmithapp.ciamlogin.com/`. User flows were empty at first; created **SignUpOrSignIn**, attached **CodeSmith.Web**. SPA redirects already had localhost + SWA origin.
- **Sandbox executor:** Confirmed **no** Entra app registration needed for `CodeSmith.Executor` / Dynamic Sessions — API uses managed identity, not user OAuth.
- **Google Cloud:** External audience, Testing + test users, Web OAuth client with Microsoft federation redirect URIs, Client ID/secret stored only in Entra (not CodeSmith config).
- **Entra:** Google built-in IdP configured; Google enabled on `SignUpOrSignIn` alongside Email with password.
- **SWA instance check:** Live bundle and Network showed authorize to `codesmithapp.ciamlogin.com` — `VITE_AAD_INSTANCE` was correct; 500208 was not a wrong-authority problem.
- **Isolation:** **Run user flow** / hosted **Sign in with Google** reached `accounts.google.com` and consent. SPA **Continue with Google** (`domain_hint=google`) failed immediately (no Google hop). Same class of CIAM desktop issue reported publicly; capital **`Google`** works where lowercase fails.
- **Google consent “continue to ciamlogin.com”:** Expected for federation — Google labels the OAuth callback host (Microsoft), not the SWA product name. Branding/custom CIAM domain only polish; not required for function.
- **CI:** Deploy SWA failed on `tsc -b` unused `beforeEach`/`afterEach` in `TerminalPanel.test.tsx` (tests still passed). Fix already on master; re-running a stale workflow run reuses old commit — need a **new** workflow_dispatch.

## Decisions & Outcomes

| Decision | Outcome |
|----------|---------|
| Keep federation in Entra only | Unchanged architecture |
| Google OAuth consent stay in Testing for smoke-test | Test users added; publish later for open signup |
| SPA Google path | `domain_hint: "Google"` in `buildGoogleLoginRequest()` |
| TerminalPanel unused imports | Removed (not delete the test file) |
| Custom “continue to CodeSmith” on Google UI | Deferred; ciamlogin.com is correct federation UX |

**Code / docs touched:**

- `CodeSmith.Web/src/auth/msalConfig.ts` — capital `Google` + comment
- Auth unit tests + `context.md` auth note
- `TerminalPanel.test.tsx` — drop unused vitest imports
- Commits on master included `d26e3b7` (domain_hint) and `e34caf4` (TerminalPanel imports) *(inferred from git log during thread)*

**Verified:** Hosted Google account chooser from CIAM; after deploy, SWA path reached `accounts.google.com` with capital-G hint.

## Open Questions / Next Steps

- Publish Google OAuth app when open self-service is desired (still Testing + test users today).
- Optional: Entra custom login domain + Google branding if product wants “continue to …” without `ciamlogin.com`.
- Confirm SWA smoke-test fully: land signed in, Bearer on protected API, new CIAM user / `ObjectId` for Google identity.
- Email path regression smoke on SWA still recommended after any auth change.

## Artifacts

| Artifact | Role | State |
|----------|------|--------|
| `Docs/Handoffs.User/2026-07-19-google-idp-sign-in-handoff.md` §8 | External runbook | Source of truth for portal steps |
| `Docs/Recaps/2026-07-19-google-idp-sign-in-design.md` | Design lock recap | Prior thread |
| `CodeSmith.Web/src/auth/msalConfig.ts` | `buildGoogleLoginRequest` | `domain_hint: "Google"` |
| Google Cloud project `CodeSmith Auth` | OAuth Web client + consent | Configured; Testing |
| Entra user flow `SignUpOrSignIn` | Email + Google; app CodeSmith.Web | Configured |
| SWA `yellow-sand-03abd5710.7.azurestaticapps.net` | Production SPA host | Redeploy for domain_hint fix |
| `.github/workflows/deploy-swa.yml` | Bakes `VITE_AAD_*` at build | Unchanged; needs new run after fixes |
