# Thread Recap — Entra External ID Verification & Usage Seam Validation

**Date:** 2026-06-27  
**Type:** verification + integration  
**Primary Goal:** Complete end-to-end validation of the Entra External ID (CIAM) bearer token authentication path and confirm the usage enforcement seam works under real identity.

## What Was Accomplished

- Created and configured a new External ID (CIAM) tenant (`codesmithapp`).
- Registered `CodeSmith.Api` as a resource server and exposed the `access` scope.
- Registered `CodeSmith.ThunderClient` as a public client for token acquisition (Authorization Code + PKCE).
- Created a test external user in the tenant.
- Fixed a critical configuration mismatch between the configured `AzureAd:Audience` and the actual `aud` claim returned in tokens.
- Successfully acquired and validated real user-delegated bearer tokens using Bruno.
- Verified the full production authentication path:
  - JWT signature, lifetime, and audience validation via `Microsoft.Identity.Web`.
  - `HttpCurrentUser` correctly extracts and surfaces the `oid` claim.
  - `UsageEnforcer` and its decorators execute under the real external user identity.
  - First-time user flow correctly creates `CreditBalance` and `UsageLedgerEntry` records.
- Confirmed the debug authentication path (`X-Debug-User-Id`) continues to function as a regression safety net.
- Identified a secondary issue in `UsageEnforcer`: the "final free action" logic is overly permissive and does not reliably enforce a hard stop once free quota is exhausted.

## Key Decisions

| Decision | Rationale / Outcome |
|----------|---------------------|
| Prioritize real bearer token verification first | Production path validated before relying on debug bypass |
| Update `AzureAd:Audience` to raw client ID | Matched the actual `aud` claim returned by Entra External ID tokens |
| Switch primary API client to Bruno | Long-term tooling preference; minor friction noted with very long headers |
| Treat quota enforcement behavior as secondary finding | Main thread goal (Entra wiring + identity seam) was completed successfully |

## Current State

- Entra External ID bearer authentication is **working end-to-end**.
- The identity seam (`HttpCurrentUser`) is correctly passing real `oid` values into business logic.
- The usage enforcement seam is functionally executing but contains a logic gap around quota exhaustion hard stops.
- Both the real user path and debug path are operational.

## Open Items

- Fix the overly permissive "final free action" behavior in `UsageEnforcer` (separate handoff prepared).
- Decide on the immediate next major project increment after the enforcement seam is stabilized (recommended: Stripe prepaid credits flow).