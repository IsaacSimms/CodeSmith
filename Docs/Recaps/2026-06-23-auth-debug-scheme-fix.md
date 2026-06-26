# Debug Authentication Scheme Fix for [Authorize] Endpoints

**Date:** 2026-06-23
**Type:** fix

## TL;DR
Implemented the minimal `DebugAuthenticationHandler` (gated on `AllowedDebugObjectIds`) plus conditional scheme registration in `Program.cs` (Development only) so that a whitelisted `X-Debug-User-Id` header satisfies `[Authorize]` attributes. This unblocks the usage enforcement seam (`UsageEnforcing*` decorators, `IUsageEnforcer`). Added unit tests. All builds and tests (203+) pass. Preserved every non-negotiable from the source handoff.

## Context & Goal
The thread started by reviewing `Handoffs/2026-06-23-auth-debug-scheme-handoff.md`. After the June 18 usage enforcement work added `[Authorize]` to LLM-spending actions (`SessionController.CreateSession/Chat`, PromptLab/SystemLab submit+chat), calls failed with `System.InvalidOperationException: No authenticationScheme was specified, and there is no DefaultChallengeScheme found.` before reaching controllers or `HttpCurrentUser`. `AddAuthentication()` had been called with no scheme. Goal: make the existing debug header satisfy auth in dev only, without removing attributes or changing any usage/ledger logic.

## Key Points Explored
- `Program.cs` had bare `AddAuthentication(); AddAuthorization();` and unconditional `UseAuthentication/UseAuthorization`.
- `HttpCurrentUser` checks `X-Debug-User-Id` against allow-list first, then claims only if `IsAuthenticated`.
- Grill-me questions (via `ask_user_question`): whether handler should also gate on `AllowedDebugObjectIds` (to preserve hardening), whether to add unit tests (project rule vs handoff spec), exact registration pattern (conditional vs override), and comment updates.
- Handler must use standard `AuthenticationHandler<AuthenticationSchemeOptions>` ctor + `HandleAuthenticateAsync`.
- Controller tests new-up controllers directly (no pipeline); need dedicated handler tests.
- Registration must set "Debug" as default scheme for unnamed `[Authorize]`.

## Decisions & Outcomes
- Handler gates success strictly on header value being in `AllowedDebugObjectIds` (exact `StringComparer.Ordinal` match); missing or unlisted → `AuthenticateResult.NoResult()`.
- Emits exactly the claims needed: `new Claim("oid", value)` and `new Claim(ClaimTypes.NameIdentifier, value)`.
- `Program.cs` restructured to:
  ```csharp
  builder.Services.AddAuthorization();
  if (builder.Environment.IsDevelopment())
      builder.Services.AddAuthentication("Debug")
          .AddScheme<AuthenticationSchemeOptions, DebugAuthenticationHandler>("Debug", options => { });
  else
      builder.Services.AddAuthentication();
  ```
- New file: `CodeSmith.Api/Services/DebugAuthenticationHandler.cs` (block title comment, class-level summary only).
- New file: `CodeSmith.Tests/Api/DebugAuthenticationHandlerTests.cs` (6 tests: success with claims verification + 5 no-result cases).
- Inline comments added/updated in `Program.cs` using project `// == Title == //` style.
- Verified: `dotnet build` clean; new tests + full suite pass with 0 failures; `[Authorize]` attributes, `HttpCurrentUser.cs`, and all usage seam code untouched.
- Followed grilled choices (user selected recommended options for gating, tests, and registration).

## Open Questions / Next Steps
- Run the smoke verification from the source handoff: start `CodeSmith.Api`, ensure `Usage:AllowedDebugObjectIds` contains the test GUID in local config, execute the provided `curl.exe` POST to `/api/session`, repeat to trigger 402, inspect `UsageLedgerEntries` and `CreditBalances` in DB, confirm no auth exception and decorators in call path.
- Long-term: replace with full Entra when ready (Debug scheme is temporary dev bridge).

## Artifacts
- Reviewed: `Handoffs/2026-06-23-auth-debug-scheme-handoff.md`
- New: `CodeSmith.Api/Services/DebugAuthenticationHandler.cs`
- Modified: `CodeSmith.Api/Program.cs` (registration + comments + using)
- New: `CodeSmith.Tests/Api/DebugAuthenticationHandlerTests.cs`
- Tests: `dotnet test` (filter + full) all green.
- Grill process used `ask_user_question` tool three times with options + recommendations.
