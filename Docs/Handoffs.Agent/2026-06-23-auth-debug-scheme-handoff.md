# Thread Handoff — Fix Debug Authentication Scheme for [Authorize] Endpoints

**Date:** 2026-06-23  
**Handoff Mode:** Implementation-to-Implementation  
**Goal for Receiving Agent:** Register a minimal "Debug" authentication scheme so that the existing `X-Debug-User-Id` header satisfies `[Authorize]` attributes in Development. This unblocks smoke testing of the protection seam without removing the attributes or changing any usage enforcement logic.

---

## 1. Current Problem (Exact State)

When calling any endpoint decorated with `[Authorize]` (e.g. `POST /api/session`, `POST /api/session/{id}/chat`, PromptLab/SystemLab spending actions), the request fails with:

```
System.InvalidOperationException: No authenticationScheme was specified, and there is no DefaultChallengeScheme found.
```

**Stack trace location:** `AuthenticationMiddleware` → `AuthorizationMiddleware` (before any controller code or `UsageEnforcing*` decorators run).

**Root cause:**  
`[Authorize]` attributes were added in the June 18 usage enforcement thread, and `UseAuthentication()` / `UseAuthorization()` are called in the pipeline. However, **no authentication scheme was ever registered** in `Program.cs`. The `HttpCurrentUser` + `X-Debug-User-Id` dev bypass logic therefore never gets a chance to execute.

This is a dev configuration gap, not a production issue.

---

## 2. Why This Must Be Fixed Before Smoke Testing the Seam

- The hardened protection seam (decorators, `IUsageEnforcer`, quota enforcement, ledger recording) lives **after** the authorization middleware.
- We cannot reach `CreateSession`, `Chat`, or any other protected LLM endpoint to exercise the 20k token quota / 402 behavior until `[Authorize]` stops throwing.
- Commenting out `[Authorize]` is a temporary workaround only. The proper fix is to make the existing debug header satisfy authorization.

---

## 3. Non-Negotiables (Do Not Violate)

- Do **not** remove or comment out any `[Authorize]` attributes permanently.
- Do **not** touch `IUsageEnforcer`, the three `UsageEnforcing*` decorators, `UsageEnforcer`, or any usage/ledger/balance logic.
- Keep the `X-Debug-User-Id` header working exactly as before for `HttpCurrentUser` (the handler should only satisfy the auth pipeline; `HttpCurrentUser` continues to be the single source of truth for `objectId`).
- The fix must be Development-friendly and easy to remove/replace later when full Entra External ID is wired.
- Preserve Clean Architecture boundaries — auth wiring lives in `CodeSmith.Api`.
- Do not implement full Entra External ID or Microsoft Identity Web in this change.

---

## 4. Recommended Implementation (Minimal & Contained)

### 4.1 New File to Create

**Path:** `CodeSmith.Api/Services/DebugAuthenticationHandler.cs`

**Purpose:** A lightweight `AuthenticationHandler` that succeeds when the `X-Debug-User-Id` header is present and creates a `ClaimsPrincipal` containing the `objectId`. This makes `[Authorize]` pass while leaving all existing `HttpCurrentUser` logic untouched.

**Key behavior the handler must have:**
- Read header `X-Debug-User-Id`.
- If present → create `ClaimsPrincipal` with at least the `"oid"` claim (and optionally `ClaimTypes.NameIdentifier` / `"sub"`) set to the header value.
- Return `AuthenticateResult.Success(ticket)`.
- If header is missing → return `AuthenticateResult.NoResult()` (so other schemes can still be tried later).
- Only intended for Development use (can be gated or left always-on for now).

### 4.2 Change in Program.cs

Inside the `WebApplication.CreateBuilder(args)` section (before `builder.Build()`), add registration for the Debug scheme and set it as default **in Development**:

```csharp
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddAuthentication("Debug")
        .AddScheme<AuthenticationSchemeOptions, DebugAuthenticationHandler>("Debug", options => { });

    // Ensure authorization is also registered (it probably already is)
    builder.Services.AddAuthorization();
}
```

Also ensure these two lines exist later in the pipeline (they should already be there from the previous auth skeleton work):

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

### 4.3 Claims the Handler Must Emit

`HttpCurrentUser` (from the June 18 work) extracts `objectId` from claims named:
- `oid`
- `objectidentifier`
- `sub`

The handler should set at minimum:

```csharp
new Claim("oid", debugUserId),
new Claim(ClaimTypes.NameIdentifier, debugUserId)
```

This guarantees `HttpCurrentUser.GetObjectId()` continues to work unchanged.

---

## 5. Verification Steps (After the Change)

1. Rebuild and run `dotnet run` in `CodeSmith.Api`.
2. From any terminal, execute:

```powershell
curl.exe -i -X POST "http://localhost:5175/api/session" `
  -H "Content-Type: application/json" `
  -H "X-Debug-User-Id: 11111111-1111-1111-1111-111111111111" `
  -d '{"difficulty":"Easy","language":"CSharp","provider":"Anthropic"}'
```

3. Expected results:
   - HTTP 201 Created (or 200 on subsequent calls) **instead of 500**.
   - New row appears in `UsageLedgerEntries` in the Azure SQL database.
   - `CreditBalances.FreeTokensUsedInWindow` updates for the test `objectId`.
4. Repeat the call with the same `objectId` until you receive `HTTP 402 Payment Required`.
5. Confirm in Container App / local logs that no `InvalidOperationException` related to authentication occurs and that the `UsageEnforcing*` decorators are in the call path.

---

## 6. Files That Will Change / Be Added

- **New:** `CodeSmith.Api/Services/DebugAuthenticationHandler.cs`
- **Modified:** `CodeSmith.Api/Program.cs` (authentication scheme registration block)
- **No changes required to:**
  - Any controller (keep the `[Authorize]` attributes)
  - `HttpCurrentUser.cs`
  - `CodeSmith.Core` or `CodeSmith.Infrastructure` (usage seam)
  - `appsettings*.json`
  - Dockerfile or deployment workflow

---

## 7. Paste-Ready Summary for Next Agent

"Picking up after the first production deploy of the hardened protection seam. The seam itself is correct, but `[Authorize]` on LLM endpoints now throws `No authenticationScheme was specified` because no scheme was ever registered.

Task: Implement a minimal `DebugAuthenticationHandler` + register it as the default scheme in Development so the existing `X-Debug-User-Id` header satisfies `[Authorize]`. 

Do **not** remove any `[Authorize]` attributes, do **not** touch `IUsageEnforcer` or the decorators, and keep `HttpCurrentUser` as the single source of truth for `objectId`.

Full context, non-negotiables, exact handler requirements, and verification steps are in the handoff document `2026-06-23-auth-debug-scheme-handoff.md`.

After the change, the smoke test (CreateSession + repeated calls until 402 + ledger verification) must pass cleanly."

---

## 8. Longer-Term Context (For Awareness Only)

- This Debug scheme is a temporary dev bridge.
- The longer-term plan (per earlier handoffs) is still to wire full Entra External ID and eventually remove/minimize reliance on the debug header.
- Once Entra is in place, the registration block can be replaced with `AddMicrosoftIdentityWebApi` and the Debug handler can be deleted or made Development-only with feature flags.

---

**End of Handoff Document**

This document contains everything needed for a coding agent to implement the fix correctly and safely in one focused session.