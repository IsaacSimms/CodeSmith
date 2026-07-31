# Google Sign-In Showed a GUID Instead of an Email

**Date:** 2026-07-30
**Type:** fix
**Environment / Systems:** Entra External ID (CIAM `codesmithapp`), MSAL SPA, Azure Static Web App, `CodeSmith.Web/src/auth/`

## TL;DR

Google-federated sign-ins rendered a raw objectId GUID in the nav bar because `AuthControls` displayed MSAL's `account.username`, which for social accounts is Entra's synthesized `{objectId}@codesmithapp.onmicrosoft.com` UPN. Fixed by adding the `email` optional ID claim (plus its OIDC Graph permission) in Entra and replacing the inline label expression with a tested `resolveAccountLabel` module. Backend identity capture was designed and approved but not yet built.

## Context & Goal

Email sign-ins showed the user's address in the top-right; Google sign-ins showed `be36f73c-1993-4e1c-8064-8a415608214…`. The Entra user object for that account was also displayed as **`unknown`**, which raised a second, larger concern from the user: *someone can authenticate with Google and there is no way to see who they are.*

## Key Points Explored

- **Display root cause.** `AuthControls.tsx:57` read `accounts[0]?.username ?? accounts[0]?.name ?? "Signed in"`. MSAL derives `username` in `AccountEntityUtils.mjs:100-105` as `preferred_username || upn || emails[0] || ""` — i.e. the UPN. Local accounts have their email as UPN so the bug was invisible; social accounts get a synthesized GUID UPN. The GUID was truncated in the UI by `max-w-[14rem] truncate`, hiding the `@codesmithapp.onmicrosoft.com` tail.
- **Secondary bug in the same line.** `??` does not fall through on `""`, and MSAL emits `""` when no claim matches — so the existing chain could render a blank span.
- **Directory investigation, and a corrected inference.** Initially assumed the `SignUpOrSignIn` user flow collected no attributes. Wrong — **Email Address was already checked**, and the user's *Properties → Contact Information → Email* held `isaacsimms11@gmail.com`. The Overview blade simply doesn't display `mail`, which is what the original inference had leaned on. Only **Display Name / Given Name / Surname** were uncollected, which is what produced the `unknown` display name.
- **This collapsed Phase 0.** No user deletion, no re-signup, no backfill — the directory was already correct on email. Only the token was missing it. The objectId, `CreditBalance` row, and free-quota window were all preserved.
- **Optional claim needed a companion permission.** Adding the `email` optional claim produced the banner *"These claims (email) require OpenID Connect Scopes to be configured through the API Permissions Page"* and a warning triangle on the claim row. Resolved by adding the Microsoft Graph delegated **`email`** (OpenId permissions) and granting admin consent.
- **Token verified empirically** rather than from docs, via a `sessionStorage` decode snippet in DevTools:
  `aud, email, exp, iat, idp, iss, name, nbf, nonce, oid, preferred_username, rh, sid, sub, tid, uti, ver`
  with `email: isaacsimms11@gmail.com`, `emails: undefined`, `name: IsaacTestGoogleAuth`.
- **Two consequences of that token.** The predicted fallback of adding `"email"` to `buildLoginRequest()` scopes was **not** needed — `msalConfig.ts` stayed untouched. And since `emails` (array form) is absent, that branch was dropped from the resolver rather than carried speculatively.
- **`name` claim ≠ directory `displayName`.** Token `name` passed through from Google as `IsaacTestGoogleAuth` while the directory still read `unknown`. The `"unknown"` guard was kept anyway since the sentinel is real for other accounts.
- **Deployment path confirmed.** `deploy-azure.yml:23` states there are no migration steps in CI, and `appsettings.Development.json:9` points local dev at the prod Azure SQL — so schema changes reach the DB via a local `dotnet ef database update`. No new mechanism needed for Phase 2.

## Decisions & Outcomes

| Decision | Outcome |
|---|---|
| Label source | Pure `resolveAccountLabel(AccountInfo)` module, not an inline expression |
| `emails` array branch | Dropped — token emits singular `email` |
| MSAL scopes | Unchanged; API permission alone was sufficient |
| Delete + re-signup the Google user | **Not needed** — directory email already populated |
| Backend identity capture seam | **Option A** — widen enforcer params to a `UserIdentity` record (chosen over an edge-capture module) |
| Cosmetic `unknown` displayName | Deferred; not on the critical path once the label prefers `email` |

**Phase 0 (Entra) — complete.** Optional ID claim `email` + Graph `email` delegated permission + admin consent.

**Phase 1 (frontend) — complete, TDD red→green→refactor.**

- New `CodeSmith.Web/src/auth/resolveAccountLabel.ts` — chain of `email` claim → email-shaped `username` (rejecting the GUID-UPN pattern) → `name` (rejecting `"unknown"`) → `"Signed in"`. Private `readEmailClaim` narrows the claim from `unknown`, since `email` is absent from MSAL's declared `TokenClaims` and arrives via the index signature; private `usable` treats blank-but-present as absent, fixing the `""` bug.
- New `resolveAccountLabel.test.ts` — 7 cases.
- `AuthControls.tsx` — import + line 57 now calls the resolver. JSX, `title` tooltip, and truncation untouched.
- `AuthControls.test.tsx` — added a federated-account case asserting the gmail renders and `/be36f73c/` is absent from the DOM.

**Verified:** `npx tsc -b` exit 0; `npx vitest run` **168/168 passing** across 19 files.

## Open Questions / Next Steps

- **Phase 2 (approved, not started).** `UserIdentity(ObjectId, Email?, DisplayName?)` record in Core; `ICurrentUser` gains `Email` + `DisplayName` (`HttpCurrentUser` reading both raw and `ClaimTypes.*`-mapped names, since Microsoft.Identity.Web leaves inbound mapping on); `CreditBalance` gains nullable `Email` + `DisplayName` stamped in `CreateNew`; `ReserveAsync`/`SettleAsync`/`ReleaseAsync` take the record instead of a bare `objectId`. Call sites: `UsageEnforcingLlmService`, `EfCreditBalanceRepository`, `EfStripeCreditStore`. Costs zero extra DB round trips — `GetSnapshotAsync` already reads the row and `PersistAsync` already writes it in one `SaveChanges`. Needs `dotnet ef migrations add` + local `database update`.
- **Phase 3.** Verify locally via `X-Debug-User-Id` (needs synthetic claims on `DebugAuthenticationHandler`), then signed-in Google on the SWA.
- **Phase 4.** Update `context.md` auth section; add a user-attributes section to `Docs/general/entra-external-id-azure-setup.md` — that doc having none is what produced the `unknown` user.
- **Optional cosmetics.** Tick Display Name / Given Name / Surname in the `SignUpOrSignIn` flow (affects future sign-ups only) and hand-edit the existing user's Display name.
- **Not yet observed in the deployed SPA.** Phase 1 is verified by tests and typecheck only; no browser confirmation of the new label was performed.

## Artifacts

| Artifact | Role | State |
|---|---|---|
| `CodeSmith.Web/src/auth/resolveAccountLabel.ts` | Label resolution module | New, passing |
| `CodeSmith.Web/src/auth/resolveAccountLabel.test.ts` | 7 unit cases | New, passing |
| `CodeSmith.Web/src/auth/AuthControls.tsx` | Nav auth controls | Edited (import + line 57) |
| `CodeSmith.Web/src/auth/AuthControls.test.tsx` | Component tests | Edited (+1 federated case) |
| `CodeSmith.Web/src/auth/msalConfig.ts` | Login requests | **Unchanged** — scope fallback not needed |
| Entra app reg `CodeSmith.Web` → Token configuration | Optional ID claim `email` | Added |
| Entra app reg `CodeSmith.Web` → API permissions | Graph delegated `email` (OpenId) | Added + admin consent |
| Entra user flow `SignUpOrSignIn` → User attributes | Email Address checked; Display Name not | Unchanged this thread |
| Entra user `be36f73c-…` | Google-federated test account | Retained, not deleted |
| DevTools `sessionStorage` decode snippet | Claim verification | Ad hoc, not committed |
