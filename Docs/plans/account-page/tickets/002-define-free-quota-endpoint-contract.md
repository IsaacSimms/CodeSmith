---
id: 002
title: Define the free-quota endpoint contract
type: grilling
status: closed
blocked_by: [001]
---

## Question

Settled constraint 2 places a new `GET /api/usage/quota` in the **enforcement** module, not
billing. Nothing about its shape is decided.

Resolve:

- Exact response DTO. `CreditBalance` holds `FreeTokensUsedInWindow`, `FreeQuotaMax`, and
  `FirstSeenUtc` — which of these cross the wire, and does the server compute expiry/active
  state or ship raw values for the client to derive? (Ticket 001 determines what is needed.)
- Where does the endpoint live — a new `UsageController`, or an existing controller? What is the
  route, given `/api/billing/*` is taken and this must not read as billing?
- `[Authorize]` or `[MeteredAi]`? It reads quota rather than spending tokens, so `[Authorize]`
  is the presumption — confirm and record why.
- What does it return for an authenticated user with **no** `CreditBalance` row yet? The row is
  created lazily on first spend, so a brand-new user has none.
- Does reading quota create a row as a side effect? (Strong presumption: no. A read endpoint
  that writes is a trap.)
- Which seam does it call — `ICreditBalanceRepository` directly, or a new read method on
  `IUsageEnforcer`? Note that `IUsageEnforcer` is currently a pure reserve/settle/release
  lifecycle and adding a read to it widens that interface.

## Answer

### Contract

```
GET /api/usage/quota          [Authorize]
200 → { freeTokensUsed: long, freeQuotaMax: long, ipConstraint: "None" | "Limited" | "Exhausted" }
```

Backed by a new read on the enforcement seam:

```csharp
Task<QuotaSnapshot> GetQuotaAsync(string objectId, string? clientIp, CancellationToken ct = default);
```

Parameters mirror `ReserveAsync`'s existing shape rather than reading `ICurrentUser` inside the
enforcer; the controller supplies both from `ICurrentUser`, which already exposes `ClientIp`
(`HttpCurrentUser.cs:44`) alongside the objectId.

### Decisions

| # | Decision | Reasoning |
|---|---|---|
| 1 | The read lands on `IUsageEnforcer`, not a new reader service or `ICreditBalanceRepository` | The enforcer already owns every input: `IUsageStore`, `UsageOptions`, the private `IpFreeTokenCap` (`:33`), and the `ComputeFreeCover` min-rule (`:279-284`). Computing remaining-quota anywhere else means re-deriving that rule, and displayed-remaining could then drift from enforced-remaining — the same defect class as ticket 008. Accepted cost: `IUsageEnforcer` is no longer a pure reserve/settle/release lifecycle, so its `<summary>` needs rewriting |
| 2 | `GET /api/usage/quota` on a new `UsageController` | Constraint 2 specifies the path. Hanging it off `BillingController` would break the seam CLAUDE.md states outright — billing never references `IUsageEnforcer` |
| 3 | `[Authorize]`, not `[MeteredAi]` | Reading quota consumes no tokens. `[MeteredAi]` would make checking your balance cost you balance |
| 4 | The read never creates a `CreditBalance` row | A GET with a write side effect is a trap, and row creation is the act that seeds `FreeQuotaMax` from config — doing it from a read would let a page visit silently fix a user's grant to whatever the config said that day |
| 5 | DTO is `{ freeTokensUsed, freeQuotaMax, ipConstraint }` — **no timestamps** | Ticket 001 eliminated the window, so `FirstSeenUtc` has nothing to say to a client. `freeQuotaMax` mirrors `CreditBalance.FreeQuotaMax`, an accurate name that survives ticket 009 — the field 009 renames is `UsageOptions.FreeMonthlyTokenQuota`, which never crosses the wire. Remaining is derivable; the client needs used+max anyway to drive `TokenUsageBar` |
| 6 | No `CreditBalance` row → synthesized zeros (`freeTokensUsed: 0`, `freeQuotaMax` from `UsageOptions`), never a 404 | Matches exactly what `CreditBalance.CreateNew` would seed, so the response is identical to what the user will have the moment they spend anything. Ticket 001 decision 4 renders this user with the bar at 0%. Note `ipConstraint` is still meaningful here — `UsageSnapshot.IpFreeTokensIssued` is independent of whether the caller has a balance row |
| 7 | IP headroom crosses the wire as a three-state enum, never a number | `/api/usage/quota` is pollable. A raw `ipFreeTokensRemaining` would turn it into a live meter of co-tenants' free-token consumption on any shared NAT; `effectiveRemaining` leaks the same value precisely when IP binds. A boolean was rejected as insufficient — the notice exists for the shared-network user, and a boolean cannot tell that user whether they have *some* tokens or *none*, which is the difference between a soft caveat and "your network is out" |
| 8 | The read is lock-free and reports persisted state, including in-flight holds | `ReserveAsync` persists an upper-bound hold *before* the call runs (`:120-127`) — that is what makes the Prompt Lab fan-out serialize correctly. A read taken mid-call therefore overstates usage by up to the full `MaxTokens` estimate until `SettleAsync` reverses it (a ~20% swing against a 20,000 grant on a 4,000-token reply). Taking `IUserUsageLock` would fix that by blocking a UI read behind a streaming LLM call — trading a hang for a cosmetic issue. The overstatement is transient and always conservative: it never shows more quota than the user has |

### Consequences for the map

- **Constrains [Design the nav-dropdown balance summary](007-design-nav-dropdown-balance-summary.md).**
  Decision 8 is only self-correcting if the client invalidates the quota query when a turn settles.
  The dropdown is visible *during* a conversation, so it must share the cache key that gets
  invalidated — otherwise it sits on an inflated number mid-chat. Ticket 007's cache-key question
  now has a right answer rather than an open one.
- `IUsageEnforcer`'s `<summary>` and `UsageEnforcer`'s class summary both describe a pure
  reserve/settle/release lifecycle. Decision 1 makes that untrue; the wording is part of the work.
- No conflict with [Remove the free-token time window from enforcement](009-remove-free-token-time-window.md):
  the DTO carries no window concept, so 009 can be resolved and implemented independently.
