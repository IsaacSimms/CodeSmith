# Google IdP Sign-In — Thread Handoff (User + Implementation)

> **Handoff Mode: Ideation → Implementation**
> **Receiving agent job: Pressure-test this design, then implement the SPA/docs side. Do not re-open locked decisions unless the user reopens them.**
> **Receiving user job: Execute §8 External Runbook (Google Cloud + Entra portal) when ready — may be a later session. SPA can ship first; this file is the companion for external wiring. That work cannot be done from the repo alone.**

**Date:** 2026-07-19  
**Branch context:** Token streaming is already on **main**. **Locked (Q11): implement Google sign-in on `main`** (most up-to-date line of development; no feature branch required unless the user says otherwise later).

---

## 1. Thread Purpose

Design multi-provider sign-in for CodeSmith: after **Sign in**, the user chooses **Continue with email** or **Continue with Google**. Identity remains **Entra External ID (CIAM)**; Google is a **federated social IdP**, not a second auth stack. This handoff freezes the design from a `/grill-me` session and gives the **user** every external (non-codebase) step required to make Google login real.

Code-side work is small (SPA chooser + MSAL `domain_hint`). **Portal work is the critical path** and is fully specified in §8.

---

## 2. Stack & Environment

| Layer | Current state |
|-------|----------------|
| SPA | React 19, MSAL (`@azure/msal-browser` / `@azure/msal-react`), Vite |
| Auth files | `CodeSmith.Web/src/auth/AuthControls.tsx`, `msalConfig.ts`, `msalInstance.ts` |
| API | .NET 8, `Microsoft.Identity.Web` + JWT Bearer (`AzureAd` section), `ICurrentUser` → Entra `oid` |
| Identity tenant | Entra External ID (CIAM) — instance shape `https://{tenant}.ciamlogin.com/` (documented as `codesmithapp.ciamlogin.com` in prior handoffs; **confirm in portal**) |
| SPA registration | `CodeSmith.Web` (PKCE public client) |
| API registration | `CodeSmith.Api` (audience / scope `api://…/access`) |
| Hosting | Azure Static Web App (SPA) + Container App (API) |
| Existing setup doc | `Docs/general/entra-external-id-azure-setup.md` |
| Prior product framing | Increment 1 deferred Google/social IdP to Increment 2 (`Docs/Recaps/2026-07-13-increment-1-frontend-hosting-auth.md`) |

---

## 3. Full Specification (locked design)

### 3.1 Architecture

| Choice | Decision |
|--------|----------|
| Identity home | **Entra External ID only** — one token type forever for this feature |
| Google placement | **Federated social IdP inside CIAM** (not Google Identity Services dual-stack, not Auth0/Clerk) |
| API validation | **Unchanged** — existing Entra JWT validation via `AddMicrosoftIdentityWebApi` |
| User key | **Unchanged** — `ICurrentUser.ObjectId` from `oid` / long-form objectidentifier / `sub` fallback |
| Billing / usage | **Unchanged** — still keyed by `ObjectId`; no schema or enforcer changes |

### 3.2 UX

1. User clicks **Sign in** in the nav (`AuthControls`).
2. A **dropdown / popover** appears with two actions:
   - **Continue with email** — existing CIAM **local email/password** (or email OTP if that is what the user flow uses). Not Microsoft personal/work account federation.
   - **Continue with Google** — CIAM federation to Google.
   - Quiet helper text under the actions (Q14): e.g. **“Use the same sign-in method next time.”**
3. Each option calls MSAL `loginRedirect` (same redirect/logout URIs as today).
4. After auth, behavior is identical to current signed-in state (username label, Sign out, API bearer via existing access-token seam).

### 3.3 MSAL behavior

| Path | Login request |
|------|----------------|
| **Email** | Current `buildLoginRequest()`: `{ scopes: [apiScope] }` — **no** IdP hard-lock for v1 |
| **Google** | Same scopes **plus** `extraQueryParameters: { domain_hint: "google" }` so CIAM skips straight to Google |

Reference (Microsoft External ID docs): social IdP direct entry uses `domain_hint=google` (also `facebook`, `apple`).

Soft targeting (v1): do **not** invest in hard-forcing email-only on the hosted page. If the CIAM page still shows a Google button after “Continue with email,” that is acceptable for v1; tighten later only if UX confuses users.

### 3.4 Account lifecycle

| Topic | Decision |
|-------|----------|
| First-time Google user | **Open self-service** — first successful Google login **creates** a CIAM user and returns a normal Entra token |
| Free quota | New Google user = **new `ObjectId`** = same free-tier treatment as email signup |
| Account linking | **Completely out of scope** — same person using email path and Google path with the same email may get **two CIAM users, two balances**. No auto-link, no app-level merge, no Graph linking in this lift. May never be added. |
| Product copy | **Locked (Q14):** quiet helper under the dropdown, e.g. “Use the same sign-in method next time.” — puts continuity on the user, not on account linking |

### 3.5 What is explicitly NOT in this lift

- Account linking / merge by email
- Real “Sign in with Microsoft” (MSA / work Entra federation)
- Dual JWT validation (Google tokens on the API)
- Replacing Entra with a broker
- Account page, Buy Credits UI, Stripe live (other Inc 2 items)
- Hard IdP lock for the email path
- Feature flag for Google (always show Email + Google; portal may lag)

### 3.6 Code-side work (agent)

**In scope:**

1. **`AuthControls.tsx`** — Sign in opens dropdown; two buttons as specified; wire email vs Google login helpers.
2. **`msalConfig.ts` (or thin helper)** — e.g. `buildLoginRequest()` and `buildGoogleLoginRequest()` with `domain_hint: "google"`.
3. **Unit tests** (Vitest / RTL) for chooser render and which login request is used (mock MSAL).
4. **Docs (locked Q13)** — `Docs/Recaps/` recap of this design/implementation; update `context.md` Authentication section to match shipped SPA behavior. This handoff remains the external runbook (no full duplicate into Entra setup doc required).
5. **No API / Core / Infrastructure identity changes** unless pressure-test finds a claims gap (unexpected).

**Out of scope for agent without user portal work:** end-to-end Google login cannot succeed until §8 is complete.

**Suggested implementation sketch (non-binding):**

```ts
// Email — existing
instance.loginRedirect(buildLoginRequest());

// Google
instance.loginRedirect({
  ...buildLoginRequest(),
  extraQueryParameters: { domain_hint: "google" },
});
```

### 3.7 Split of labor

| Who | What |
|-----|------|
| **User (you)** | Google Cloud OAuth client + Entra Google IdP + enable Google on user flow + smoke-test (§8) |
| **Agent** | SPA dropdown, MSAL `domain_hint`, tests, repo docs |
| **Neither in this lift** | Account linking, Microsoft account IdP, billing UI |

---

## 4. What Is NOT Yet Decided (non-load-bearing)

All load-bearing product/architecture decisions are locked. Remaining are implementer/polish defaults:

| Item | Default if unstated |
|------|---------------------|
| Exact dropdown styling | Match existing nav button styles (Tailwind). **Locked (Q15): text only** — no Google brand button assets; official Google button (option C) deferred to a separate iteration. |
| Dropdown dismiss | Click outside / Escape / after choosing |
| OAuth cancel / error UX | MSAL/CIAM default redirect; no custom error page required for v1 |
| Google button available before portal ready | **Locked (Q12): always show both options** — no feature flag. No external users yet; dead Google path until §8 is fine. This handoff remains the companion for later portal work. |
| Whether CIAM hosted page still lists Google after email path | Accept for v1 |
| Automated tests | **Locked (Q9): unit/component only** — Vitest + RTL, mock MSAL; no live Google/Playwright auth in CI. Manual smoke via §8.4 after portal. |
| Google OAuth consent publishing | **Locked (Q10): In production** as soon as IdP is wired — personal project; open Google self-service is fine. If Google blocks with verification warnings, fall back to test users temporarily. |

---

## 5. Key Decisions & Rationale

| Decision | Rationale |
|----------|-----------|
| Federate Google into CIAM (not dual OAuth) | API already validates Entra JWTs; usage/billing keyed by `oid`. Second token stack is multi-week waste. |
| App-owned chooser + IdP hint | Matches desired UX; `domain_hint=google` is supported for External ID social IdPs. |
| Soft email path | Hard local-only lock is fiddly; not needed for first ship. |
| Labels: Email / Google (not “Microsoft”) | Current path is CIAM local email accounts, not Microsoft account SSO. Honest labeling. |
| No account linking | May never ship. Split balances accepted; Q14 helper text puts “same method next time” on the user. |
| Open Google self-signup | Same as open email signup; enforcer already meters `ObjectId`. |
| User owns portal secrets | Google client secret lives in Entra IdP config, never in the SPA or git. |

---

## 6. Current State

- **Design:** locked (this document).
- **Code (SPA):** shipped on `master` — Sign in dropdown, `buildGoogleLoginRequest` (`domain_hint=google`), unit tests, `context.md` updated.
- **Portal:** Google IdP **not** assumed configured — user must run §8 when ready.
- **Email sign-in:** still works via MSAL → CIAM (Increment 1 path).
- **Debug auth:** Development `X-Debug-User-Id` path unchanged and orthogonal.

---

## 7. Next Steps (Ordered)

### For the user (external — do first or in parallel with SPA)

1. Complete **§8 External Runbook** (Google Cloud → Entra IdP → user flow → test users).
2. Verify Google sign-in once via raw CIAM/MSAL path (even before dropdown ships): e.g. temporary `domain_hint` or use the Google button on the CIAM hosted page after Google is enabled on the user flow.
3. Record Tenant ID, subdomain, and that Google is on the correct user flow for `CodeSmith.Web`.
4. When SPA ships: redeploy SWA if needed; smoke-test both chooser paths on localhost and production hostname.

### For the implementation agent

1. Pressure-test §3 only for technical gaps (claims, user-flow binding, MSAL API). Do not reopen locked product choices.
2. Implement dropdown + Google `domain_hint` + tests.
3. Update `context.md` Authentication (SPA chooser + Google federation; API unchanged). Recap already at `Docs/Recaps/2026-07-19-google-idp-sign-in-design.md` — amend Outcomes if implementation drifts.
4. Do **not** implement account linking or API auth dual-stack.
5. Hand back to user for §8 verification if portal not done yet.

---

## 8. External Runbook — Everything Outside the Codebase

**Goal:** Customers can authenticate with Google; CIAM issues the same style of access token your API already accepts. No CodeSmith code deploy is required for federation itself — only for the in-app Email/Google chooser.

**You need:** access to the **Entra external (CIAM) tenant**, and a **Google account** that can create a Google Cloud project.

### 8.0 Gather values first (write them down)

From the **external tenant** in [Microsoft Entra admin center](https://entra.microsoft.com) (switch directory to the **CodeSmith CIAM** tenant, not the workforce tenant):

| Value | Where to find it | Example shape |
|-------|------------------|---------------|
| **Directory (tenant) ID** | Entra ID → Overview | GUID |
| **Tenant subdomain / primary domain** | Overview — e.g. `something.onmicrosoft.com` or custom; CIAM login host is often `https://{name}.ciamlogin.com/` | `codesmithapp` → `https://codesmithapp.ciamlogin.com/` |
| **User flow name** | Entra ID → External Identities → User flows | e.g. `SignUpSignIn` |
| **SPA app (`CodeSmith.Web`) is on that user flow** | User flow → Applications | Must include the SPA app registration |

Confirm SPA redirect URIs already include:

- `https://localhost:5173` (and `http://localhost:5173` if you use HTTP locally)
- Production SWA origin (e.g. `https://….azurestaticapps.net` or custom domain)

Federation does **not** add SPA redirect URIs for Google — Google redirects to **Microsoft**, not to your SPA.

---

### 8.1 Create Google Cloud OAuth client

Follow Microsoft’s customer guide: [Add Google as an identity provider - Microsoft Entra External ID](https://learn.microsoft.com/en-us/entra/external-id/customers/how-to-google-federation-customers).

1. Open [Google Cloud Console](https://console.cloud.google.com/).
2. Create (or select) a project — e.g. `CodeSmith Auth`.
3. **APIs & services** → **OAuth consent screen**
   - User type: **External**
   - App name: e.g. `CodeSmith`
   - User support email: your email
   - **Authorized domains:** add at least:
     - `ciamlogin.com`
     - `microsoftonline.com`
   - Developer contact: your email
   - Save through scopes if prompted (default profile/email/openid is enough for sign-in)
4. **Credentials** → **Create credentials** → **OAuth client ID**
   - Application type: **Web application**
   - Name: e.g. `Microsoft Entra External ID`
5. **Authorized redirect URIs** — add **all** of the following, substituting your values  
   (`<tenant-ID>` = Directory GUID, `<tenant-subdomain>` = e.g. `codesmithapp` or the `….onmicrosoft.com` left label as Microsoft’s doc describes):

   ```
   https://login.microsoftonline.com
   https://login.microsoftonline.com/te/<tenant-ID>/oauth2/authresp
   https://login.microsoftonline.com/te/<tenant-subdomain>.onmicrosoft.com/oauth2/authresp
   https://<tenant-ID>.ciamlogin.com/<tenant-ID>/federation/oidc/accounts.google.com
   https://<tenant-ID>.ciamlogin.com/<tenant-subdomain>.onmicrosoft.com/federation/oidc/accounts.google.com
   https://<tenant-subdomain>.ciamlogin.com/<tenant-ID>/federation/oauth2
   https://<tenant-subdomain>.ciamlogin.com/<tenant-subdomain>.onmicrosoft.com/federation/oauth2
   ```

   Use the **exact** list from the Microsoft doc if the portal UI has updated — prefer the live doc over this handoff if they disagree.

6. Create → **copy Client ID and Client secret** immediately (secret is shown once).
7. **OAuth consent publishing**
   - **Locked (Q10): set consent screen to In production** once the OAuth client exists (personal project; open Google signup is intended).
   - If Google shows an “unverified app” warning or blocks login, temporarily add **Test users** and/or complete Google verification — then return to production when clear.

**Do not** put the Google client secret in CodeSmith appsettings, user secrets for the API, or any `VITE_*` variable. Only Entra stores it.

---

### 8.2 Configure Google as an identity provider in Entra External ID

1. Entra admin center → switch to **external/CIAM tenant**.
2. **Entra ID** → **External Identities** → **All identity providers** (or **Identity providers**).
3. **Built-in** tab → **Google** → **Configure**.
4. Name: e.g. `Google`.
5. Paste **Client ID** and **Client secret** from §8.1.
6. **Save**.

(PowerShell alternative exists via Microsoft Graph `New-MgIdentityProvider` with `identityProviderType = "Google"` — portal is enough.)

---

### 8.3 Attach Google to the user flow used by CodeSmith.Web

1. **Entra ID** → **External Identities** → **User flows**.
2. Open the user flow that **`CodeSmith.Web` is assigned to** (critical: wrong flow = SPA never shows Google).
3. **Identity providers** (under Settings).
4. Under **Other Identity Providers**, enable **Google**.
5. Ensure **Email Accounts** (email + password and/or OTP) remain enabled — that is **Continue with email**.
6. **Save**.

If `CodeSmith.Web` is not on this user flow: **User flow** → **Applications** → **Add application** → select `CodeSmith.Web`.

---

### 8.4 Smoke-test federation (before or after SPA dropdown)

**Option A — CIAM hosted page (no code change)**  
1. Start local SPA (`npm run dev`) or open production SWA.  
2. Sign in with current single button (or after chooser: either path).  
3. On the CIAM page, use **Google** if visible.  
4. Complete Google consent.  
5. Land back on the SPA signed in; call a protected API (create session, etc.).

**Option B — force Google with domain_hint (after SPA change)**  
Chooser → Continue with Google → should skip to Google more directly.

**Success criteria:**

- [ ] Browser completes Google consent and returns to SPA origin with MSAL account populated.
- [ ] Network tab: API calls include `Authorization: Bearer …`.
- [ ] Protected endpoint succeeds (e.g. `POST /api/session` → 201).
- [ ] In Entra **Users**, a new customer user appears for the Google identity (or existing if re-login).
- [ ] Note the user’s **Object ID** — that is the usage/billing key (`ICurrentUser.ObjectId`).

**Failure checklist:**

| Symptom | Likely cause |
|---------|----------------|
| `redirect_uri_mismatch` at Google | Missing/wrong URI in Google Cloud OAuth client (§8.1) |
| Google not shown on CIAM page | Google not enabled on **this** user flow, or SPA app not on that flow |
| `access_denied` / app blocked | Consent screen in Testing; add Test user |
| SPA returns but API 401 | Unrelated to Google — wrong audience/scope/`AzureAd` config (existing Entra wiring) |
| Two balances for “same person” | Expected — no linking; email user ≠ Google user |

---

### 8.5 What you do **not** need to change externally

| Item | Why |
|------|-----|
| API `AzureAd` ClientId / Audience | Still Entra-issued tokens for the same API app |
| `VITE_AAD_*` values | Same SPA app registration and scopes |
| Google client ID in frontend | SPA never talks to Google directly; CIAM does |
| Stripe / billing webhook | Identity still `ObjectId` |
| SQL schema | No new identity tables |
| Container App secrets for Google | Google secret is only in Entra IdP config |

---

### 8.6 Optional ops / hygiene

- Store Google client secret in a password manager; if rotated, update Entra Google IdP config the same day.
- Document which Google Cloud project owns production auth (billing alerts on that project).
- When SWA custom domain is added later: **SPA** redirect URIs need the new origin — Google OAuth redirect URIs usually **do not** (still Microsoft endpoints).
- If you create a **second** CIAM tenant later, you need a second Google OAuth client (or carefully shared config) — not in scope today (single external tenant).

---

### 8.7 Order of operations summary

```
[You] Google Cloud OAuth client + secrets
        ↓
[You] Entra → configure Google IdP
        ↓
[You] Entra → enable Google on CodeSmith user flow
        ↓
[You] Add Google accounts as OAuth test users (if consent = Testing)
        ↓
[Agent] SPA Sign in dropdown + domain_hint=google  (can be parallel after design lock)
        ↓
[You + Agent] E2E: Email path still works; Google path works; API accepts token
```

---

## 9. Must-Knows for the New Thread

- User prefers **TDD**, concise pushback, **UL** terms when design language is used (Module, Seam, Adapter, etc.).
- **Do not** invent a second auth scheme on the API.
- **Do not** “helpfully” add account linking.
- Debug header auth in Development remains; do not break it.
- Prior Inc 1 handoff treated Google as Inc 2 — this **is** that social IdP slice only, not full Inc 2 billing UI.
- User asked for this file under `Docs/Handoffs.User` specifically so **they** can run portal work; agent handoffs for code may still live under `Docs/Handoffs.Agent` if a later session needs a pure agent resume.

---

## 10. Relevant Artifacts

| Path | Role | State |
|------|------|--------|
| `CodeSmith.Web/src/auth/AuthControls.tsx` | Sign in / Sign out UI | Exists — single button today |
| `CodeSmith.Web/src/auth/msalConfig.ts` | MSAL config + `buildLoginRequest` | Exists — no `domain_hint` yet |
| `CodeSmith.Web/src/auth/msalInstance.ts` | PCA bootstrap | Exists |
| `CodeSmith.Api/Program.cs` | Entra JWT + Dev Debug scheme | No change expected |
| `CodeSmith.Api/Services/HttpCurrentUser.cs` | `oid` / `sub` resolution | No change expected |
| `Docs/general/entra-external-id-azure-setup.md` | Entra setup guide | Needs Google phase after implementation |
| `Docs/Recaps/2026-07-13-increment-1-frontend-hosting-auth.md` | Deferred Google to Inc 2 | Historical |
| This file | Design lock + user external runbook | Authoritative for external work |
| `Docs/Recaps/2026-07-19-google-idp-sign-in-design.md` | Design grill recap | Written; matches locked decisions |
| `context.md` | Architecture ground truth | Update auth section when SPA ships |

---

## Paste into new thread

**For implementation agent:**

> Here's a fully-specified feature design from an ideation session. Your job is to pressure-test this spec first — challenge assumptions, surface gaps, identify contradictions — then implement it once we've aligned. Here's the spec: [paste this document or point at `Docs/Handoffs.User/2026-07-19-google-idp-sign-in-handoff.md`]. Start by grilling the design before writing any code. User owns §8 portal work; you own SPA chooser + `domain_hint` + tests + docs.

**For you (portal only):**

> I'm executing the Google IdP external runbook in `Docs/Handoffs.User/2026-07-19-google-idp-sign-in-handoff.md` §8. Help me only if I hit a portal error — do not start SPA implementation unless I ask.
