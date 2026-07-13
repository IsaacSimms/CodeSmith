# Entra External ID (CIAM) — Azure Setup Guide for CodeSmith

**Date:** 2026-06-27  
**Audience:** You (portal steps) + any agent verifying bearer auth afterward  
**Prerequisite:** `CodeSmith.Api` Entra wiring is already implemented; `AzureAd` section in `appsettings.json` has placeholders.

---

## Overview

CodeSmith's API validates JWT bearer tokens via `Microsoft.Identity.Web` + `AddMicrosoftIdentityWebApi`. Configuration binds to the `AzureAd` section. Entra **External ID** (CIAM) uses `*.ciamlogin.com` as the authority — not `login.microsoftonline.com`.

**Goal:** Provision CIAM, register the API, acquire a user-delegated token, and verify `POST /api/session` succeeds with `Authorization: Bearer {token}` while `ICurrentUser` resolves the correct `oid`.

---

## What You Need at the End

| Value | Config key | Example shape |
|-------|------------|---------------|
| Tenant ID (GUID) | `AzureAd:TenantId` | `aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee` |
| CIAM domain | `AzureAd:Instance` | `https://codesmith.ciamlogin.com/` |
| API app Client ID | `AzureAd:ClientId` | GUID |
| Token audience | `AzureAd:Audience` | `api://{clientId}` or custom scope URI |

Real values go in **user secrets** (local) and **Key Vault** (Container Apps). Do not commit real IDs to source control.

---

## Phase 1 — Create or Access CIAM Tenant

1. Open [Microsoft Entra admin center](https://entra.microsoft.com).
2. Switch to or create an **External ID for customers** tenant.
   - If creating new: follow the "Create a tenant" → "External ID" wizard.
   - Note the **Tenant ID** (GUID) from **Overview**.
   - Note the **Primary domain** (e.g. `codesmith.onmicrosoft.com`).
3. Your CIAM authority host will be: `https://{tenant-name}.ciamlogin.com/`  
   (The `{tenant-name}` portion is shown in the External ID tenant overview — confirm in portal before filling config.)

---

## Phase 2 — Register the API Application

1. In the CIAM tenant: **Applications** → **App registrations** → **New registration**.
2. Settings:
   - **Name:** `CodeSmith.Api`
   - **Supported account types:** Accounts in this organizational directory only (single tenant)
   - **Redirect URI:** leave blank for now (API resource, not a login client)
3. After creation, copy the **Application (client) ID** — this is `AzureAd:ClientId`.
4. Copy the **Directory (tenant) ID** — this is `AzureAd:TenantId`.

### Expose an API

1. Open the app → **Expose an API**.
2. Set **Application ID URI** — default `api://{clientId}` is fine. This becomes your audience base.
3. **Add a scope:**
   - Scope name: `access` (or `api.access`)
   - Who can consent: Admins only (for dev)
   - Admin consent display name: `Access CodeSmith API`
   - Admin consent description: `Allows the app to access CodeSmith API on behalf of the signed-in user`
4. Note the full scope string: `api://{clientId}/access` (or whatever you named it).

### Set `AzureAd:Audience`

Use one of:
- `api://{clientId}` — validates tokens with that audience
- `api://{clientId}/access` — if you scope narrowly

Start with `api://{clientId}` unless token validation fails, then try the scoped form.

---

## Phase 3 — Register a Client App for Thunder Client (PKCE)

Thunder Client needs an app registration that can run Authorization Code + PKCE and request your API scope.

1. **New registration:**
   - **Name:** `CodeSmith.ThunderClient` (or `CodeSmith.DevClient`)
   - **Redirect URI:** Platform = **Public client/native**, URI = `https://oauth.thunderclient.com/oauth/callback`  
     (Thunder Client's standard OAuth callback — confirm in TC OAuth settings UI)
2. Copy this app's **Client ID** — used in Thunder Client OAuth config (not in `AzureAd:ClientId` unless you use the same app for both, which is not recommended).
3. **API permissions** → Add permission → **My APIs** → `CodeSmith.Api` → select `access` scope → Grant admin consent.

### Optional: Enable public client flow

If token acquisition fails:
- App registration → **Authentication** → enable **Allow public client flows** → Yes.

---

## Phase 3b — Register SPA Client (`CodeSmith.Web`) for MSAL

The hosted React app uses a separate SPA registration (not the API app, not Thunder Client).

1. **New registration:**
   - **Name:** `CodeSmith.Web`
   - **Supported account types:** single tenant (this CIAM directory)
   - **Redirect URI:** Platform = **Single-page application (SPA)**
     - `https://localhost:5173`
     - `https://<your-swa-hostname>` (e.g. `https://yellow-sand-….azurestaticapps.net`)
2. Copy **Application (client) ID** → GitHub Variable / local `.env.local` as `VITE_AAD_CLIENT_ID`.
3. **API permissions** → **My APIs** → `CodeSmith.Api` → delegated `access` → **Grant admin consent**.
4. No client secret (PKCE public client).

### Frontend / SWA env (build-time `VITE_*`)

| Variable | Value |
|----------|--------|
| `VITE_API_BASE_URL` | Container App URL (empty locally so Vite proxy applies) |
| `VITE_AAD_CLIENT_ID` | SPA app client ID |
| `VITE_AAD_TENANT_ID` | CIAM tenant GUID |
| `VITE_AAD_INSTANCE` | `https://{tenant}.ciamlogin.com/` |
| `VITE_AAD_API_SCOPE` | `api://{api-client-id}/access` |

Deploy: GitHub Action **Deploy Static Web App** (`workflow_dispatch`) with secret `AZURE_STATIC_WEB_APPS_API_TOKEN`.

Local: copy `CodeSmith.Web/.env.example` → `.env.local` and fill SPA values; leave `VITE_API_BASE_URL` unset.

---

## Phase 4 — Create a Test User

1. In CIAM tenant: **External Identities** → **Users** (or **All users**) → **New user**.
2. Create a test customer account (email + password).
3. You'll sign in as this user when acquiring tokens via Thunder Client.

---

## Phase 5 — Configure CodeSmith Locally

### User secrets (recommended)

From `CodeSmith.Api` directory:

```powershell
cd C:\CodeSmith\CodeSmith\CodeSmith.Api

dotnet user-secrets set "AzureAd:Instance" "https://YOUR_TENANT.ciamlogin.com/"
dotnet user-secrets set "AzureAd:TenantId" "YOUR-TENANT-GUID"
dotnet user-secrets set "AzureAd:ClientId" "YOUR-API-APP-CLIENT-ID"
dotnet user-secrets set "AzureAd:Audience" "api://YOUR-API-APP-CLIENT-ID"
```

User secrets override `appsettings.json` placeholders at runtime.

### Verify Development config still has debug allow-list

`appsettings.Development.json` must still contain:

```json
"Usage": {
  "AllowedDebugObjectIds": [
    "11111111-1111-1111-1111-111111111111"
  ]
}
```

---

## Phase 6 — Thunder Client OAuth 2.0 Setup

1. Open Thunder Client in VS Code → create/open an environment.
2. Add OAuth 2.0 configuration:

| Field | Value |
|-------|-------|
| Grant type | Authorization Code |
| Auth URL | `https://{tenant}.ciamlogin.com/{tenantId}/oauth2/v2.0/authorize` |
| Access Token URL | `https://{tenant}.ciamlogin.com/{tenantId}/oauth2/v2.0/token` |
| Client ID | `{ThunderClient app client ID}` |
| Scope | `api://{apiClientId}/access openid profile offline_access` |
| PKCE | **Enabled** |
| Redirect URI | `https://oauth.thunderclient.com/oauth/callback` |

3. Click **Get Access Token** → sign in as test user → copy token.
4. Thunder Client stores the token for requests in that environment.

### Decode and inspect the token

Paste the JWT into [jwt.ms](https://jwt.ms) and confirm:
- `oid` claim present (this is what `HttpCurrentUser` reads first)
- `aud` matches your `AzureAd:Audience`
- `iss` is your CIAM authority
- Token is not expired

---

## Phase 7 — API Verification Checklist

Start the API:

```powershell
cd C:\CodeSmith\CodeSmith\CodeSmith.Api
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run
```

API listens on `http://localhost:5175`.

### Test 1 — Bearer auth (new path)

**Request:**
- `POST http://localhost:5175/api/session`
- Header: `Authorization: Bearer {your-token}`
- **No** `X-Debug-User-Id` header
- Body:
  ```json
  {
    "difficulty": "Easy",
    "language": "CSharp",
    "provider": "Anthropic"
  }
  ```

**Expected:**
- `201 Created` (first call with quota) or `402 Payment Required` (quota exhausted)
- **Not** `401 Unauthorized`
- If `401`: check audience, tenant ID, Instance URL, token expiry

**Verify identity seam:**
- Query `UsageLedgerEntries` / `CreditBalances` in Azure SQL
- `objectId` column should match the `oid` from your JWT (not the test GUID)

### Test 2 — Debug auth regression (existing path)

**Request:**
- Same POST, but replace bearer with `X-Debug-User-Id: 11111111-1111-1111-1111-111111111111`
- No bearer token

**Expected:**
- `201` or `402` (same as before Entra wiring)
- `objectId` in DB = test GUID

### Test 3 — Unauthenticated (negative)

**Request:** Same POST, no auth headers.

**Expected:** `401 Unauthorized`

---

## Phase 8 — Production (Container Apps + Key Vault)

1. Add Key Vault secrets (or Container Apps secret references):
   - `AzureAd--Instance`
   - `AzureAd--TenantId`
   - `AzureAd--ClientId`
   - `AzureAd--Audience`
2. Map secrets to environment variables in `ca-codesmith-api-001`.
3. Ensure `ASPNETCORE_ENVIRONMENT` is **not** `Development` in production — Debug scheme will not register.
4. Deploy via existing GitHub Actions workflow.
5. Verify production returns `401` without bearer; valid CIAM token returns `201`/`402`.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| `401` with valid-looking token | Wrong `Audience` | Match `aud` claim in jwt.ms to `AzureAd:Audience` |
| `401` immediately | Wrong `Instance` / tenant ID | Confirm `*.ciamlogin.com` URL and tenant GUID |
| `401` only in Production | Debug header sent without bearer | Production has no Debug scheme — use bearer only |
| `500` after auth passes | SQL Serverless paused | Resume DB in Azure Portal (Error 40613) |
| Token has no `oid` | Wrong token type | Use user-delegated PKCE token, not client-credentials |
| API won't start | Invalid config at startup | Check user secrets formatting; ensure GUIDs are valid |

---

## Auth Architecture Reference (Post-Wiring)

```
Request
  → AuthenticationMiddleware
      → Bearer scheme (default): validates JWT via Microsoft.Identity.Web
      → Debug scheme (Dev only): X-Debug-User-Id + AllowedDebugObjectIds
  → AuthorizationMiddleware
      → DefaultPolicy: Bearer (+ Debug in Dev)
  → Controller [Authorize]
  → ICurrentUser (HttpCurrentUser): reads oid claim from authenticated principal
  → UsageEnforcing* decorators → IUsageEnforcer
```

**Non-negotiable:** Do not read claims or headers in controllers. `ICurrentUser` is the identity seam.

---

## Related Docs

- `Docs/Recaps/2026-06-27-entra-external-id-wiring.md` — what was built
- `Docs/Handoffs.Agent/2026-06-27-entra-external-id-handoff.md` — agent handoff for verification phase
- `Docs/Handoffs.Agent/2026-06-23-auth-debug-scheme-handoff.md` — original debug handler spec
- `Docs/Recaps/2026-06-25-recap-thread.md` — protection seam verification