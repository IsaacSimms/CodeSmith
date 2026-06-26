# Usage Quota Overhaul to 20k with 48h Window and IP Caps

**Date:** 2026-06-20
**Type:** implementation

## TL;DR
Replaced the 100k monthly free quota with a strict 20k lifetime-within-48h-window per objectId, added dual IP aggregate caps (20k per objectId + 60k total per IP), hardened the X-Debug-User-Id bypass to an explicit allow-list only, downgraded PromptLab and SystemLab evaluations to the fast model while consuming free quota, and implemented a lenient "last action" gate so users can complete the call that exhausts their free allowance. All token-burning paths (including problem creation) count. Goal: give exactly one demo experience (problem gen + guidance + one submit) while tightly controlling personal API spend and raising the bar on account farming.

## Context & Goal
Long grill-me session started from concern that 100k free was too generous for personal budget, easy Microsoft account creation would allow farming, and "refresh page" or new sessions might bypass. User wanted ~1 full workflow before forcing payment. Prompt/System Lab submits using accurate model on every test input were identified as especially expensive. Requirements evolved to: short time window, IP-based friction (both per-object+IP and shared per-IP pool), header lock-down, model downgrade for free tier, and allow the exhausting call to complete.

## Key Points Explored
- Token burn per flow: problem gen (accurate ~2-4k), guidance (fast), one Prompt Lab submit with 4-6+ test inputs (N fast simulate + N accurate evaluate) easily 15-20k+.
- Farming vectors: new Entra accounts trivial; current X-Debug header allowed arbitrary values even in prod.
- Refresh/new session does not reset DB-backed CreditBalance or IP caps.
- Composite IP caps add friction for same-network abusers without VPNs.
- 48h window from first objectId sighting (global) + lost-forever semantics makes repeated demos costly.
- Downgrading only evaluations during free window preserves paid quality while cutting demo cost.
- Lenient gate in CheckAndReserve: allow if any free room remains on object or IP, then record actuals.
- All spend paths already routed through UsageEnforcingLlmService decorator.

## Decisions & Outcomes
- Free: 20_000 per objectId, 60_000 aggregate per IP.
- Window: 48 hours from FirstSeenUtc on objectId; free exhausted forever after.
- IP handling: normalized client IP (via ForwardedHeaders) for both per-object cap and aggregate; dual locks (object + ip:...) for safety.
- Header: X-Debug-User-Id only if value exactly matches UsageOptions.AllowedDebugObjectIds (empty in prod).
- Model policy: Fast for PromptLab/SystemLab evaluate while inside free window; Accurate otherwise. Problem gen stays Accurate.
- Lenient gate implemented and verified.
- New entities: IpFreeUsage + IIpFreeUsageRepository + Ef impl.
- CreditBalance fields repurposed (FreeTokensUsedInWindow, FirstSeenUtc).
- ForwardedHeaders middleware + config added.
- Full build + 203 tests green.
- Implementation followed detailed plan/checklist produced at end of design phase.

## Open Questions / Next Steps
- User requested separate dev testing instructions after implementation (to be printed to terminal and documented).
- Potential future: shorten window further, tighten IP normalization/hashing, add alerts on IP cap exhaustion, or migrate to real Entra claims only.

## Artifacts
- Updated files: UsageOptions.cs (20k default + AllowedDebugObjectIds), CreditBalance.cs, UsageEnforcer.cs (core logic + IP/window), UsageEnforcingLlmService.cs (downgrade), HttpCurrentUser.cs (header list + ClientIp), Program.cs (ForwardedHeaders), DbContext + new IpFreeUsage model + repo + interface.
- New: C:\CodeSmith\CodeSmith\Recaps\2026-06-20-usage-quota-20k-window-ip-caps.md (this file), IpFreeUsage.cs, IIpFreeUsageRepository.cs, EfIpFreeUsageRepository.cs.
- Previous design artifacts: extensive grill-me thread + final plan document in chat history.
- Tests updated and passing in CodeSmith.Tests\Infrastructure\Usage\UsageEnforcerTests.cs.