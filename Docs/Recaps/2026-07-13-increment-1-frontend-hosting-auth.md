# Increment 1 — Frontend Hosting + Minimal Entra MSAL

**Date:** 2026-07-13  
**Type:** implementation  
**Environment / Systems:** Azure Static Web Apps, Azure Container Apps, Entra External ID (CIAM), React/Vite SPA, .NET 8 API, GitHub Actions

## TL;DR

Shipped Increment 1: hosted `CodeSmith.Web` on Azure Static Web Apps with MSAL (redirect) against Entra External ID, hybrid API client (prod absolute URL + Bearer), billing success/cancel stubs, and a manual SWA deploy workflow. Prod Bearer auth, SQL, and usage enforcer were proven (402). Post-deploy `NetworkError` was CORS; fixed via `AllowedCorsOrigins__*` on the Container App.

## Context & Goal

Prior work left usage enforcement and Stripe billing on the API, with Entra wired server-side and Bruno/local debug auth working. Frontend had no auth and was never hosted. The ideation handoff scoped Increment 1 tightly: SWA hosting + thin MSAL wiring only — no account page, Google IdP, Stripe live, or seam changes. Goal: external users can open the SPA, sign in with Microsoft/CIAM, and call protected APIs.

## Key Points Explored

- **Grill-me locked design:** hybrid API base URL (C), new SPA app registration, Sign in/out without route guards, MSAL local+prod, Stripe stub routes, in-repo `workflow_dispatch` SWA deploy, Azure-only CORS/Stripe URLs, msal-browser + msal-react + redirect, GitHub Variables for `VITE_*`, prod Bearer smoke before trusting MSAL, `apiClient` unit tests only.
- **Portal path:** SWA `swa-codesmith-prod-centralus-001` (`https://yellow-sand-03abd5710.7.azurestaticapps.net`); SPA `CodeSmith.Web` with SPA platform redirects; prod `AzureAd__*` on Container App (was empty before this thread).
- **Prod smoke progression:** missing path → 404; empty `Xai__ApiKey` → 500; blank `ConnectionStrings__CodeSmithDb` → connection-string format error; fixed → **402** (auth + enforcer + SQL OK). Logs showed valid JWT audience `1aebbadb-e40c-45ba-a958-8bdbb48f2968`.
- **Frontend implementation:** `setAccessTokenProvider` + `resolveApiUrl`; MSAL bootstrap in `main.tsx`; `AuthControls` in Layout; `/billing/success|cancel`; `deploy-swa.yml`; `staticwebapp.config.json`; `.env.example`; Entra doc Phase 3b.
- **Post-deploy NetworkError:** bundle correctly targeted Container App; OPTIONS/GET initially lacked `Access-Control-Allow-Origin`; after `AllowedCorsOrigins__0/1` on Container App, CORS headers present and browser calls succeeded.

## Decisions & Outcomes

| Decision | Outcome |
|----------|---------|
| Scope stay tight | No account UI, Google, live Stripe, or enforcer/billing/ICurrentUser changes |
| Hybrid API URL | Empty `VITE_API_BASE_URL` locally (Vite proxy); absolute Container App URL in prod build |
| New SPA app reg | Separate from API and Thunder Client; PKCE + delegated `access` |
| Auth UX | Nav Sign in/out; Bearer when signed in; no route guards |
| Deploy | `Deploy Static Web App` workflow_dispatch only |
| Prod config | AzureAd, Xai key, SQL connection string, CORS, Stripe redirect URLs on Container App env |
| Verification | Workflow green; sign-in works; API usable after CORS fix |

## Open Questions / Next Steps

- **Increment 2 (deferred):** Google/social IdP, thin account page + Buy Credits, Stripe live mode, success/cancel polish, broader multi-user testing.
- **Ops hygiene:** rotate any secrets pasted in chat (SWA deploy token, API keys in portal screenshots); prefer secret refs over plain env for keys long-term.
- **Optional:** System Lab `POST sessions` / `run` still lack `[Authorize]` (left alone in Inc 1).
- Confirm Bruno/local `X-Debug-User-Id` path remains the dev testing default (Development only).

## Artifacts

| Artifact | Location | State |
|----------|----------|-------|
| API client + tests | `CodeSmith.Web/src/lib/apiClient.ts`, `apiClient.test.ts` | Shipped |
| MSAL / auth UI | `CodeSmith.Web/src/auth/*`, `Layout.tsx`, `main.tsx` | Shipped |
| Billing stubs | `features/billing/components/BillingResultPage.tsx`, routes in `App.tsx` | Shipped |
| SWA config | `CodeSmith.Web/public/staticwebapp.config.json` | Shipped |
| SWA deploy workflow | `.github/workflows/deploy-swa.yml` | Shipped |
| Env template | `CodeSmith.Web/.env.example` | Shipped |
| Entra SPA notes | `Docs/general/entra-external-id-azure-setup.md` (Phase 3b) | Updated |
| SWA resource | `swa-codesmith-prod-centralus-001` | Live |
| Agent handoff | `Docs/Handoffs.Agent/2026-07-13-increment-1-frontend-hosting-completion-handoff.md` | Companion |
