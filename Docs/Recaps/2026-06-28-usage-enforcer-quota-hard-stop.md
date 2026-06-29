# UsageEnforcer Quota Hard-Stop Fix

**Date:** 2026-06-28
**Type:** fix
**Environment / Systems:** CodeSmith.Api (local, port 5175), Azure SQL (`CreditBalances`, `IpFreeUsages`, `UsageLedgerEntries`), debug user `11111111-1111-1111-1111-111111111111`

## TL;DR

Fixed the broken free-quota seam in `UsageEnforcer`: removed the IP-only bypass that let object-exhausted users through, enforced a true hard stop at zero remaining quota on both object and IP caps, implemented partial free-then-paid deduction in `RecordActualAsync`, and replaced misleading "final free action" logs. Eight boundary unit tests added; all 236 backend tests pass. Manual smoke test with full 20k free quota confirmed the check gate allows the request; recording failed separately due to a missing `ProviderCostUsd` DB column (tracked in another thread).

## Context & Goal

Thread picked up from [`Docs/Handoffs.Agent/2026-06-27-usage-enforcer-quota-bug-handoff.md`](../Handoffs.Agent/2026-06-27-usage-enforcer-quota-bug-handoff.md). Debug tester `11111111-1111-1111-1111-111111111111` could still get `201 Created` even when SQL showed `RemainingFreeTokens = 0` (manually set `FreeQuotaMax = FreeTokensUsedInWindow = 20000`). Logs showed repeated "Permitting final free action" messages even when ~16k tokens remained.

Goal: make quota exhaustion deterministic — return `402 Payment Required` before any LLM call once free quota is exhausted, without breaking intentional partial-free behavior when estimate exceeds remaining headroom.

## Key Points Explored

- **IP-only bypass (primary bug for zero-remaining case):** `CheckAndReserveAsync` line 82 permitted calls when `objectFreeRem == 0` but `ipRem > 0 && FreeQuotaMax > 0`, routing overflow to `PaidCreditsBalance` (sometimes negative).
- **Misleading "final free action" logs:** Lenient branch fired whenever strict pre-check failed (`objectFreeRem >= est AND ipRem >= est`) but *any* object or IP headroom remained — not a one-shot "last call." With ~16k object quota left, strict check could fail on tight IP pool while lenient path still allowed every request.
- **RecordActual all-or-nothing:** If `freeRem < actualTokens`, entire `chargeUsd` hit paid credits instead of consuming partial free first.
- **Grill-me design session:** User accepted mid-flow blocking at limit. Chose refined Option B (hard block at zero on both caps, partial-free only when both have headroom) plus partial free-then-paid split in `RecordActual`.

## Decisions & Outcomes

| Decision | Outcome |
|----------|---------|
| Lenient gate semantics | Permit partial-free only when `windowActive && objectFreeRem > 0 && ipRem > 0`; require paid credits for estimate overflow |
| Remove IP-only bypass | Object quota at zero → `402` even if IP pool has room |
| RecordActual partial split | Consume `min(freeRem, ipRem, actualTokens)` as free; debit proportional `chargeUsd` for paid remainder |
| Log messages | Replaced "Permitting final free action" with accurate partial-free wording |
| Manual verification | User-owned; agent runs automated tests only |

**Implementation:**

- [`CodeSmith.Infrastructure/Services/Usage/UsageEnforcer.cs`](../../CodeSmith.Infrastructure/Services/Usage/UsageEnforcer.cs) — three-path gate (strict free / paid / partial-free with overflow check); `ComputeFreeCover` and `SplitTokensProportionally` helpers; partial `RecordActualAsync`
- [`CodeSmith.Tests/Infrastructure/Usage/UsageEnforcerTests.cs`](../../CodeSmith.Tests/Infrastructure/Usage/UsageEnforcerTests.cs) — 8 new boundary tests (exhausted object quota, exhausted IP quota, IP-bypass removal, partial-free permit/block, window expired, post-record hard stop, partial RecordActual split)

**Verification:**

- `dotnet test --filter FullyQualifiedName~UsageEnforcer` → 12/12 passed
- Full `dotnet test` → 236/236 passed
- User smoke test with `FreeTokensUsedInWindow = 0`, `FreeQuotaMax = 20000`, `FirstSeenUtc` reset: quota check passed (no 402), LLM generated, `RecordActualAsync` failed on `Invalid column name 'ProviderCostUsd'` — schema drift, not quota logic

## Open Questions / Next Steps

- **User manual verification (this thread):** Re-test with `FreeTokensUsedInWindow = FreeQuotaMax = 20000` → expect `402` after API restart with new build.
- **`ProviderCostUsd` column (separate thread):** EF model has the field; initial migration does not. Blocks ledger writes until Azure SQL is migrated. User actively working on this elsewhere.
- **Optional:** Update README "lenient last action" wording to match refined partial-free semantics.

## Artifacts

- Handoff (input): [`Docs/Handoffs.Agent/2026-06-27-usage-enforcer-quota-bug-handoff.md`](../Handoffs.Agent/2026-06-27-usage-enforcer-quota-bug-handoff.md)
- Recap (this file): `Docs/Recaps/2026-06-28-usage-enforcer-quota-hard-stop.md`
- Changed source: `UsageEnforcer.cs`, `UsageEnforcerTests.cs`