# Thread Handoff — CodeSmith (For Isaac)

**Date:** 2026-06-25

This document summarizes the key changes and important working practices coming out of this thread so you can continue effectively.

---

## What Was Accomplished

### 1. Protection Seam is Now Verified
- The `UsageEnforcer` + three decorator classes are working correctly in the live system.
- First successful end-to-end test (`POST /api/session`) proved:
  - Authentication via debug header works.
  - Usage is recorded to `UsageLedgerEntries`.
  - `CreditBalances` is updated with tokens used.
  - Cost calculation and free-first logic function as designed.

### 2. Major Blocking Issues Resolved
- Fixed the authentication scheme registration problem that was causing 500 errors on all protected endpoints.
- Moved the SQL Server from the wrong resource group into `rg-codesmith-prod-centralus-001`.
- Resumed the paused Serverless database so it is now `Online`.

### 3. Cleanup
- Confirmed the temporary debug step has been removed from the deploy workflow.
- Verified the old SQL login (`CloudSA67f19294`) no longer exists as a database user (no further action needed).

---

## Important Things to Know Going Forward

### Development & Testing Workflow (Current State)

**Local Development Against Production DB:**
- You use `X-Debug-User-Id` header (value must be in `AllowedDebugObjectIds` in your local config).
- The API runs on `http://localhost:5175` (HTTP, not HTTPS).
- Use **Thunder Client** (VS Code) for testing — it is much more reliable than curl one-liners in PowerShell.
- Always restart the API after changing `appsettings.Development.json` or user secrets.

**Database Access:**
- Use Azure Portal → Query editor on `db-codesmith-prod-centralus-001`.
- Useful queries:
  ```sql
  SELECT TOP 10 * FROM UsageLedgerEntries ORDER BY TimestampUtc DESC;
  SELECT * FROM CreditBalances WHERE ObjectId = 'your-test-object-id';
  ```

**Smoke Testing the Protection Seam:**
- Hit protected endpoints (e.g. `POST /api/session`).
- Watch for 201 on success and 402 when quota is exhausted.
- Check both the API logs and the database tables after each call.

### Key Architectural Decisions You Should Internalize

- **Protection seam is sacred**: All LLM calls must go through the `UsageEnforcing*` decorators. Do not call raw LLM services from controllers or services.
- `ICurrentUser` is the single source of truth for `objectId`. Do not read headers or claims directly in business logic.
- Billing features must be built as a **separate module** that only credits `PaidCreditsBalance`. It must not modify usage enforcement logic.
- We are currently in a "debug header" dev mode. Full Entra External ID is planned but not yet active.

### Infrastructure Notes

- SQL Server lives in `rg-codesmith-prod-centralus-001` (recently moved there).
- Database is Serverless and can auto-pause. You may need to resume it occasionally via Azure CLI or Portal.
- Deployments go through GitHub Actions → ACR → Container App using Managed Identity.

---

## Recommended Next Steps (Your Choice)

1. **Drive the seam to 402** — Keep calling the same endpoint with the same test `objectId` until you get a clean `402`. This gives full confidence in quota enforcement.
2. **Proceed to Stripe prepaid credits module** (the locked next increment).
3. **Add a simple internal "grant credits" tool** first (easier than full Stripe for testing the paid path).
4. **Continue cleanup** or start wiring more of Entra External ID.

---

## One-Sentence Summary of Current State

The cost protection foundation is now built and verified. The system is stable and ready for the next major increment (Stripe billing) or deeper verification of the existing seam.

You can pick up from here with confidence that the core usage enforcement is working as designed.