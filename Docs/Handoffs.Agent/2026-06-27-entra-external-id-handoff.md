# Thread Handoff — Entra External ID Wiring (Post-Implementation)

**Date:** 2026-06-27

> **Handoff Mode: Implementation**
> **Receiving agent job: Resume and continue — provision Azure CIAM resources, configure secrets, verify bearer-token auth path**

---

## 1. Thread Purpose

This thread grill-me'd, planned, and implemented Entra External ID wiring for `CodeSmith.Api`. The original ideation handoff specified Debug as the default scheme in Development; the grill-me session revised that to **production-first Bearer auth** with Debug as a Development-only side path. Implementation is complete and tested. The receiving agent's job is Azure provisioning and end-to-end bearer-token verification — not re-implementing auth wiring.

---

## 2. Stack & Environment

- **Backend:** .NET 8, ASP.NET Core Web API (`CodeSmith.Api` only for auth changes)
- **Auth library:** `Microsoft.Identity.Web` 3.8.4
- **Default auth scheme:** `Bearer` (`JwtBearerDefaults.AuthenticationScheme`) in all environments
- **Dev side path:** `DebugAuthenticationHandler` + `X-Debug-User-Id` (allow-listed via `Usage:AllowedDebugObjectIds`)
- **Identity seam:** `ICurrentUser` → `HttpCurrentUser` (claims-only, no header bypass)
- **Protection seam:** `IUsageEnforcer` + three `UsageEnforcing*` decorators — **verified, non-negotiable**
- **Hosting:** Azure Container Apps + Managed Identity; Key Vault for secrets
- **Local API:** `http://localhost:5175` (HTTP)
- **Testing:** Thunder Client; test GUID `11111111-1111-1111-1111-111111111111`
- **Azure state:** **Greenfield** — no CIAM tenant provisioned yet; `appsettings.json` has placeholders

---

## 3A. What Was Accomplished

1. **Grill-me session** — resolved 8 design branches; superseded original handoff's "Debug as default" with production-first Bearer model.
2. **Plan artifact** — `~/.grok/plans/codesmith-entra-external-id-wiring.md`.
3. **Package** — added `Microsoft.Identity.Web` 3.8.4 to `CodeSmith.Api.csproj`.
4. **Config scaffold** — `AzureAd` section in `appsettings.json` with CIAM-shaped placeholders.
5. **`Program.cs`** — Bearer default + `AddMicrosoftIdentityWebApi`; Debug scheme chained on base `AuthenticationBuilder` (not MIW return type); environment-specific `DefaultPolicy` (Dev: Bearer+Debug, Prod: Bearer only).
6. **`HttpCurrentUser.cs`** — claims-only; removed `X-Debug-User-Id` header bypass and `UsageOptions` dependency.
7. **`DebugAuthenticationHandler.cs`** — comment updated to reflect coexistence with Entra (logic unchanged).
8. **Tests** — new `CodeSmith.Tests/Api/HttpCurrentUserTests.cs` (9 tests); **224/224 pass**.
9. **Manual smoke** — debug header reaches spending endpoints; unauthenticated requests return 401.

---

## 4A. Current State

**Done:**
- Production-ready auth wiring in code
- Debug path regression-verified (auth passes; DB may return 500 if SQL Serverless is paused — unrelated)
- Unit test coverage for claims-only `HttpCurrentUser`

**Not done:**
- No CIAM tenant or app registration exists
- `AzureAd` config values are placeholders (`00000000-...`)
- No real bearer token has been tested against the API
- Thunder Client OAuth 2.0 not configured
- Key Vault / Container Apps secrets not updated with real `AzureAd` values
- Frontend MSAL not started

**You are here:** Code is ready. Azure provisioning is the blocking next step.

---

## 5. Key Decisions & Rationale

| Decision | Rationale |
|----------|-----------|
| Bearer as default auth scheme (not Debug) | User wants production-ready code as the standard; debug is the side addition |
| Multi-scheme `DefaultPolicy` in Dev only | Keeps Thunder Client `X-Debug-User-Id` working without making Debug the default auth scheme |
| Claims-only `HttpCurrentUser` | Single path through auth handler → claims → `ICurrentUser`; deeper module |
| Greenfield scaffold (placeholders in source control) | No tenant exists; real values go in user secrets / Key Vault |
| Thunder Client OAuth 2.0 (PKCE) for bearer testing | User-delegated tokens carry `oid`; keeps single verification tool |
| Inline auth in `Program.cs` | ~30 lines; matches existing style; no premature abstraction |
| `AuthenticationBuilder` before `AddScheme` | MIW return type lacks `AddScheme`; must chain Debug on base builder |
| No protection seam changes | June 25 verification is sacred; any touch requires full re-verification |

---

## 6. Blockers & Open Questions

| Item | Status | Next step |
|------|--------|-----------|
| No CIAM tenant | **Blocking** bearer verification | Follow `Docs/general/entra-external-id-azure-setup.md` |
| Placeholder `AzureAd` config | Expected | Fill after app registration |
| `Audience` format for CIAM | Open during provisioning | Likely `api://{clientId}` or custom scope — confirm in token after setup |
| SQL Serverless paused during smoke | Environmental | Resume DB in Azure Portal if testing full 201/402 path |

---

## 7. Next Steps (Ordered)

1. **Provision Azure CIAM** — tenant, API app registration, exposed scope, test user. See `Docs/general/entra-external-id-azure-setup.md`.
2. **Fill `AzureAd` config** — user secrets locally; Key Vault reference in Container Apps for production.
3. **Configure Thunder Client OAuth 2.0** — Authorization Code + PKCE against CIAM endpoints.
4. **Bearer smoke test** — `POST http://localhost:5175/api/session` with `Authorization: Bearer {token}`, no debug header. Expect 201 (or 402 if quota exhausted).
5. **Verify identity seam** — confirm `oid` in JWT matches `UsageLedgerEntries` / `CreditBalances` objectId.
6. **Re-run debug regression** — confirm `X-Debug-User-Id` still works in Development alongside bearer.
7. **Update Container Apps** — deploy with real `AzureAd` secrets; verify Production returns 401 without bearer (Debug not registered).

---

## 8. Must-Knows for the New Thread

- **Do not re-implement auth wiring** — it is done. Focus on Azure provisioning and verification.
- **Do not touch the protection seam** — `IUsageEnforcer`, decorators, spending endpoints.
- **`ICurrentUser` is claims-only** — debug identity must pass through `DebugAuthenticationHandler` auth, not raw header reads.
- **Bearer is default; Debug is Dev-only side scheme** — superseded June 27 ideation handoff default.
- **Test GUID** `11111111-1111-1111-1111-111111111111` must remain in `Usage:AllowedDebugObjectIds` for debug smoke tests.
- **Client-credentials tokens are wrong for `ICurrentUser` testing** — use user-delegated tokens (PKCE).
- **User conventions:** direct output, block title comments, TDD, edit-in-place, Ubiquitous Language (seam, module, adapter).

---

## 9. Relevant Artifacts

| File | State |
|------|-------|
| `CodeSmith.Api/Program.cs` | Complete — auth block lines 49–77 |
| `CodeSmith.Api/Services/HttpCurrentUser.cs` | Complete — claims-only |
| `CodeSmith.Api/Services/DebugAuthenticationHandler.cs` | Complete — logic unchanged |
| `CodeSmith.Api/appsettings.json` | Placeholder `AzureAd` section |
| `CodeSmith.Tests/Api/HttpCurrentUserTests.cs` | Complete — 9 tests |
| `CodeSmith.Tests/Api/DebugAuthenticationHandlerTests.cs` | Unchanged — 6 tests |
| `~/.grok/plans/codesmith-entra-external-id-wiring.md` | Implementation plan (executed) |
| `Docs/general/entra-external-id-azure-setup.md` | Azure provisioning guide |
| `Docs/Recaps/2026-06-27-entra-external-id-wiring.md` | Thread recap |

---

> **Paste into new thread:**
> "Picking up from a previous session. Entra External ID wiring is implemented in CodeSmith.Api (Bearer default, Debug Dev side path, claims-only HttpCurrentUser). Azure CIAM tenant is not provisioned yet. Here's the handoff: [paste this document]
> Next job: follow the Azure setup guide, fill secrets, verify bearer token on POST /api/session. Do not touch the protection seam. Confirm you have context before starting."