# Entra External ID Wiring — Grill-Me, Plan, and Implementation

**Date:** 2026-06-27
**Type:** implementation
**Environment / Systems:** .NET 8 ASP.NET Core (`CodeSmith.Api`), Azure Container Apps (production target), local Development via Thunder Client

## TL;DR

This thread pressure-tested the Entra External ID increment via grill-me, revised the original handoff from "Debug as default" to **production-first Bearer auth**, wrote an implementation plan, and shipped the wiring. `Microsoft.Identity.Web` is registered with Bearer as the default scheme; Debug remains a Development-only side path via a multi-scheme authorization policy. `HttpCurrentUser` is now claims-only. All 224 tests pass; debug smoke tests still reach spending endpoints. Azure CIAM tenant provisioning and real bearer-token verification remain deferred — documented in separate artifacts.

## Context & Goal

After June 25 protection seam verification, the next increment was Entra External ID wiring. The thread opened with an ideation handoff specifying additive/parallel auth (keep Debug as default). A grill-me session revised that to production-ready semantics: Bearer is standard, Debug is the side addition. The user approved the plan, then directed implementation.

## Key Points Explored

- **Grill-me decisions (8 questions):**
  - Default auth scheme: **Bearer** (not Debug) — user chose production-first over original handoff default
  - Dev `[Authorize]`: multi-scheme default authorization policy (`Bearer` + `Debug`)
  - `HttpCurrentUser`: **claims-only** — remove header bypass; identity flows through auth handler → claims
  - Azure state: **greenfield** — no CIAM tenant or app registration yet
  - Done boundary: wiring + unit tests + debug regression; Azure portal steps deferred
  - Post-provisioning token acquisition: **Thunder Client OAuth 2.0 (PKCE)**
  - Tests: `HttpCurrentUserTests` + existing handler tests (no JWT integration tests)
  - Registration location: inline in `Program.cs`

- **Implementation gotcha resolved:** `AddMicrosoftIdentityWebApi` returns `MicrosoftIdentityWebApiAuthenticationBuilderWithConfiguration`, which does not expose `AddScheme`. Fix: capture the `AuthenticationBuilder` from `AddAuthentication()` first, call `AddMicrosoftIdentityWebApi` on it, then chain `AddScheme` for Debug on the same builder.

- **Protection seam untouched:** No changes to `IUsageEnforcer`, decorators, controllers, or spending logic.

## Decisions & Outcomes

| Decision | Outcome |
|----------|---------|
| Bearer default in all environments | `Program.cs` registers `JwtBearerDefaults.AuthenticationScheme` + `AddMicrosoftIdentityWebApi` |
| Debug as Dev side path | `"Debug"` scheme + multi-scheme `DefaultPolicy` only when `IsDevelopment()` |
| Claims-only `ICurrentUser` adapter | Header bypass removed from `HttpCurrentUser`; `UsageOptions` dependency removed |
| `Microsoft.Identity.Web` 3.8.4 | Added to `CodeSmith.Api.csproj` |
| CIAM config scaffold | `AzureAd` section in `appsettings.json` with placeholder GUIDs and `*.ciamlogin.com` Instance |
| Unit tests | New `HttpCurrentUserTests.cs` (9 tests); `DebugAuthenticationHandlerTests` unchanged |
| Full test suite | **224/224 passed** |
| Manual smoke (debug path) | `X-Debug-User-Id` request reached `SessionController` + usage decorators (401 without header) |

## Open Questions / Next Steps

1. Provision Entra External ID (CIAM) tenant and API app registration — see `Docs/general/entra-external-id-azure-setup.md`
2. Fill real `AzureAd` values in user secrets (local) and Key Vault (Container Apps)
3. Configure Thunder Client OAuth 2.0 and verify bearer token on `POST /api/session`
4. Confirm `oid` claim in token matches `ICurrentUser.ObjectId` in usage ledger
5. Deferred: remove/feature-flag Debug handler in Production; frontend MSAL; Stripe billing increment

## Artifacts

| Artifact | Location | State |
|----------|----------|-------|
| Implementation plan | `~/.grok/plans/codesmith-entra-external-id-wiring.md` | Complete |
| Recap (this file) | `Docs/Recaps/2026-06-27-entra-external-id-wiring.md` | Complete |
| Agent handoff | `Docs/Handoffs.Agent/2026-06-27-entra-external-id-handoff.md` | Complete |
| Azure setup guide | `Docs/general/entra-external-id-azure-setup.md` | Complete |
| Modified: `Program.cs` | Auth registration | Shipped |
| Modified: `HttpCurrentUser.cs` | Claims-only | Shipped |
| Modified: `DebugAuthenticationHandler.cs` | Comment update | Shipped |
| Modified: `appsettings.json` | `AzureAd` scaffold | Shipped |
| Modified: `CodeSmith.Api.csproj` | `Microsoft.Identity.Web` | Shipped |
| New: `HttpCurrentUserTests.cs` | 9 unit tests | Shipped |