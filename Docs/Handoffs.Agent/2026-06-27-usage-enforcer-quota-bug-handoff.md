# Thread Handoff — UsageEnforcer Quota Exhaustion Hard-Stop Bug

**Date:** 2026-06-27  
**Handoff Mode:** Implementation  
**Target:** Coding agent with full repo access

## Problem

When a user (particularly the debug tester account) approaches or reaches their free token quota, `UsageEnforcer` repeatedly enters a "Permitting final free action" state and continues to allow requests instead of returning `402 Payment Required`.

The debug tester account still had substantial quota remaining (`FreeTokensUsedInWindow = 3678` out of `FreeQuotaMax = 20000`) when this behavior was observed across multiple calls.

## Observed Symptoms

- Repeated log messages:  
  `"Permitting final free action for 11111111-1111-1111-1111-111111111111 (will exhaust remaining free quota or IP cap)."`
- Requests continue to return `201 Created`.
- `CreditBalances.PaidCreditsBalance` is updated (sometimes going negative).
- `UsageLedgerEntries` continue to be created with `free:0`.

## Expected Behavior

Once free quota is exhausted (or after the single "final allowed call"), subsequent requests for that `objectId` should be rejected with `InsufficientQuotaException` (mapped to `402 Payment Required`) **before** any LLM call is executed.

## Relevant Code

Primary file:
- `CodeSmith.Infrastructure/Services/Usage/UsageEnforcer.cs` (especially `CheckAndReserveAsync` and the final-free-action logic)

Supporting files:
- `EfCreditBalanceRepository.cs`
- `EfUsageLedgerRepository.cs`
- `UsageEnforcing*` decorator classes
- Any constants or configuration related to `FreeQuotaMax` or window reset logic

## Task

1. Analyze the current "final free action" branch and the conditions under which it is entered and exited.
2. Implement a clear, reliable hard stop so that once free quota is exhausted, further calls are rejected with `402` before reaching the LLM.
3. Ensure the fix does not break legitimate "last allowed call" behavior if that path is intentional.
4. Add or strengthen tests around the quota exhaustion boundary (both for `objectId`-based and any IP-based logic).
5. Verify the fix works for both real Entra users and the debug header path.

## Guidance

- Focus on making the exhaustion check deterministic and easy to reason about.
- The reproduction steps and logs from 2026-06-27 are available in the thread for context if needed.
- Do not modify the public `IUsageEnforcer` interface or the decorator structure.
- Keep changes minimal and targeted.

## Success Criteria

- Debug tester (and real users) receive `402 Payment Required` after free quota is exhausted.
- No repeated "final free action" calls once the limit is reached.
- Existing tests continue to pass; new boundary tests are added.