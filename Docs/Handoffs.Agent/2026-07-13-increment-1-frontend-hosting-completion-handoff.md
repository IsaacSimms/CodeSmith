# Thread Handoff Document

> **Handoff Mode: Implementation**  
> **Receiving agent job: Resume from a completed Increment 1. Do not re-open locked Inc 1 design unless the user reopens it. Default next product work is Increment 2 (social IdP, thin account/billing UI, Stripe live) or ops hardening only if asked.**

---

### 1. Thread Purpose (2–4 sentences)

This thread pressure-tested and implemented **Increment 1**: host `CodeSmith.Web` on Azure Static Web Apps and wire minimal Entra External ID auth from the browser (MSAL redirect + Bearer on API calls). Backend seams (`IUsageEnforcer`, billing module, `ICurrentUser`, LLM decorators) were not modified. Prod was brought from “AzureAd never configured on Container App” through Bearer + enforcer success (402), then CORS so the hosted SPA could call the API. Inc 1 is complete and manually verified by the user.

---

### 2. Stack & Environment

- Frontend: React 19, TypeScript, Vite 6, TanStack Query, React Router, Tailwind v4 — `CodeSmith.Web`
- Auth client: `@azure/msal-browser` + `@azure/msal-react` (v5), redirect only
- Backend: .NET 8 ASP.NET Core — `ca-codesmith-api-001` (Container Apps, Central US)
- Identity: Entra External ID (CIAM) tenant **CodeSmith** — `https://codesmithapp.ciamlogin.com/`
- SPA host: Azure Static Web App `swa-codesmith-prod-centralus-001`  
  URL: `https://yellow-sand-03abd5710.7.azurestaticapps.net`
- API host: `https://ca-codesmith-api-001.icysea-31eca31b.centralus.azurecontainerapps.io`
- CI/CD: `deploy-azure.yml` (API, workflow_dispatch); `deploy-swa.yml` (frontend, workflow_dispatch)
- Dev: Windows, Bruno OAuth for prod smoke; local API still supports `X-Debug-User-Id` only when `ASPNETCORE_ENVIRONMENT=Development`
- Stripe: still sandbox; Success/Cancel URLs intended to point at SWA `/billing/success|cancel`

---

### 3A. What Was Accomplished

1. **Grill-me** locked Inc 1 design (hybrid API URL, new SPA app reg, Sign in/out only, MSAL local+prod, Stripe stubs, SWA workflow_dispatch, Azure-only CORS/Stripe config, msal-react + redirect, GitHub Variables for `VITE_*`, prod Bearer gate before MSAL trust, `apiClient` unit tests only).
2. **Azure SWA** created: `swa-codesmith-prod-centralus-001`, Source=Other, deploy token → GitHub Secret `AZURE_STATIC_WEB_APPS_API_TOKEN`.
3. **Entra SPA app** `CodeSmith.Web`: SPA platform redirects for localhost:5173 + SWA origin; API permission `access` + admin consent.
4. **Container App prod config** filled (had been empty secrets blade):  
   `AzureAd__Instance/TenantId/ClientId/Audience`, `Xai__ApiKey` (and other provider keys), `ConnectionStrings__CodeSmithDb` (Entra passwordless / Active Directory Default), later `AllowedCorsOrigins__0/1`, Stripe success/cancel URLs *(user confirmed)*.
5. **Prod Bearer smoke:** token acquisition in Bruno; path must be `/api/session`; progression empty Xai key → empty/malformed SQL string → **402 Insufficient quota** with JWT audience validated in logs.
6. **Code shipped:**
   - `apiClient`: `resolveApiUrl`, `setAccessTokenProvider`, Bearer when token present
   - `src/auth/msalConfig.ts`, `msalInstance.ts`, `AuthControls.tsx`
   - Layout Sign in/out; `main.tsx` MSAL bootstrap + optional `MsalProvider`
   - Billing stubs + routes `/billing/success`, `/billing/cancel`
   - `public/staticwebapp.config.json` navigation fallback
   - `.github/workflows/deploy-swa.yml` (build with `vars.VITE_*`, deploy dist, `skip_app_build`)
   - `.env.example`; Entra setup doc Phase 3b
   - Minimal TS fixes: MessageBubble role map; PromptLab session mock `dynamicInputsGenerated`
7. **Deploy + verify:** SWA workflow succeeded; user signed in on hosted app; CORS fixed NetworkError; user considered Inc 1 done.

---

### 4A. Current State

- **Inc 1 product path is live:** hosted SPA + MSAL + protected API calls (after CORS).
- **Backend seams unchanged** and working under real Entra `oid` in prod when quota allows (402 = expected hard stop).
- **Local dev:** relative `/api` + Vite proxy when `VITE_API_BASE_URL` unset; MSAL if `.env.local` filled; Bruno debug header still Dev-only on API.
- **Not done (by design — Inc 2):** Google/social IdP, account page, Buy Credits UI, Stripe live keys/webhook polish, multi-user scale testing, optional `[Authorize]` gaps on System Lab start / run code.
- **Security note:** deploy token and API keys appeared in chat/screenshots during portal work — rotation recommended *(user action)*.

You are here: Increment 1 closed. Next work is Increment 2 planning/implementation or unrelated tasks — not re-implementing Inc 1.

---

### 5. Key Decisions & Rationale

| Decision | Rationale |
|----------|-----------|
| Hybrid API base URL (prod absolute, local relative) | Two hosts; avoid SWA reverse-proxy complexity; keep Vite proxy |
| New SPA app registration | Separate public client from API resource and Thunder Client |
| Sign in/out only (no route guards) | Tight scope; public GETs still work without token |
| SPA never sends `X-Debug-User-Id` | Keep debug bypass server-side Dev-only; Bruno stays on API |
| Azure config only for CORS/Stripe/AzureAd | No prod hostnames committed in appsettings |
| `AllowedCorsOrigins__N` indexed env vars | ASP.NET array binding; single-string form fails silently |
| Connection string: Entra passwordless | Matches MI + local AD Default; not SQL auth |
| Redirect login (not popup) | Reliable with CIAM / browser restrictions |
| Leave System Lab start / run unauthenticated | Explicitly out of Inc 1 scope |

---

### 6. Blockers & Open Questions

| Item | Status | Next if needed |
|------|--------|----------------|
| User free quota 402 on test account | Expected | Credits / new user / wait for window — not an auth bug |
| Secret leakage in thread | Open | Rotate SWA deploy token, provider keys if exposed |
| Container App CORS | Resolved | Keep SWA origin exact (no trailing slash) |
| Array env binding footgun | Documented | Always `__0`, `__1` for CORS list |
| Inc 2 scope | Not started | User must re-grill or hand off ideation before large build |

---

### 7. Next Steps (Ordered)

1. **Do not re-implement Inc 1.** Confirm with user if next is Increment 2 or something else.
2. If **Increment 2:** pressure-test then implement social IdP (Google), thin account UI + Buy Credits against existing `/api/billing/*`, Stripe live mode + prod webhook secret, polish billing pages; keep billing ↔ enforcer separation.
3. If **ops only:** rotate secrets; verify Stripe Success/Cancel env still match SWA; optional Key Vault for provider keys.
4. If **auth hardening:** consider `[Authorize]` on System Lab `POST sessions` and tutoring `run` (product decision).
5. Local contributor setup: `.env.local` from `.env.example`; empty `VITE_API_BASE_URL`; SPA redirect still `https://localhost:5173`.

---

### 8. Must-Knows for the New Thread

- User: **tight scope**, TDD when building, prefer edit-in-place, block titles `// == Title == //`, no member-level `/// <summary>`, direct pushback preferred, no empty affirmations.
- **ICurrentUser** is sole identity seam; never read headers/claims in controllers for user id.
- **Bearer** is default everywhere; Debug scheme **Development only**.
- API audience/client for validation is **API app** (`1aebbadb-e40c-45ba-a958-8bdbb48f2968` in this env); SPA client id is **only** for MSAL `VITE_AAD_CLIENT_ID`.
- Scope string: `api://1aebbadb-e40c-45ba-a958-8bdbb48f2968/access` (or current Expose an API full scope).
- Hosted fetch URL must be Container App, not SWA `/api/...`.
- CORS: browser Origin must match allow-list exactly; symptoms = `NetworkError when attempting to fetch resource`.
- Deploy SWA after any `VITE_*` or frontend auth change; CORS-only fixes need API revision only.
- Do not commit real secrets; `appsettings.Development.json` historically had keys — treat carefully.

---

### 9. Relevant Artifacts

| Artifact | Path | State |
|----------|------|-------|
| API client | `CodeSmith.Web/src/lib/apiClient.ts` | Complete |
| API client tests | `CodeSmith.Web/src/lib/apiClient.test.ts` | Complete (base URL + Bearer) |
| MSAL config/bootstrap | `CodeSmith.Web/src/auth/*` | Complete |
| Auth UI | `CodeSmith.Web/src/auth/AuthControls.tsx` in `Layout` | Complete |
| Billing stubs | `CodeSmith.Web/src/features/billing/components/BillingResultPage.tsx` | Complete (minimal) |
| SWA workflow | `.github/workflows/deploy-swa.yml` | Complete |
| SWA SPA fallback | `CodeSmith.Web/public/staticwebapp.config.json` | Complete |
| Env template | `CodeSmith.Web/.env.example` | Complete |
| Entra guide | `Docs/general/entra-external-id-azure-setup.md` | Phase 3b added |
| Thread recap | `Docs/Recaps/2026-07-13-increment-1-frontend-hosting-auth.md` | Complete |
| Ideation handoff (prior) | User-provided Inc 1 planning docs in session | Superseded by this completion handoff for “what next” |

**GitHub Variables (expected names):**  
`VITE_API_BASE_URL`, `VITE_AAD_CLIENT_ID`, `VITE_AAD_TENANT_ID`, `VITE_AAD_INSTANCE`, `VITE_AAD_API_SCOPE`  

**GitHub Secret:** `AZURE_STATIC_WEB_APPS_API_TOKEN`

---

**Paste into new thread:**

"Picking up from a previous session. Here's the handoff: [paste the entire document above]

Confirm you have context and flag anything unclear before we continue. Increment 1 is complete — do not re-scope it unless I ask. Default next work is Increment 2 or whatever I specify."
