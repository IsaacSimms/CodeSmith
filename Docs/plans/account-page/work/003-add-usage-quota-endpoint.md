---
id: 003
title: Add the free-quota read endpoint
status: done
implements: [002]
depends_on: [001]
---

## Goal

Ship `GET /api/usage/quota` on a new `UsageController`, backed by a new read on `IUsageEnforcer`, so
the account page and nav dropdown display remaining free quota computed by the module that enforces
it.

## Constraints

- `GET /api/usage/quota` `[Authorize]` on a new `UsageController`; not on `BillingController` —
  billing never references `IUsageEnforcer` —
  [Define the free-quota endpoint contract](../tickets/002-define-free-quota-endpoint-contract.md) #2, #3
- The read lands on `IUsageEnforcer` as
  `Task<QuotaSnapshot> GetQuotaAsync(string objectId, string? clientIp, CancellationToken ct = default)`,
  not on a new reader service or `ICreditBalanceRepository` — #1
- Response is `{ freeTokensUsed, freeQuotaMax, ipConstraint }`. No timestamps — #5
- `ipConstraint` crosses the wire as `"None" | "Limited" | "Exhausted"`, never a number — #7
- The read never creates a `CreditBalance` row; a missing row synthesizes zeros with `freeQuotaMax`
  from `UsageOptions`, never a 404 — #4, #6
- The read is lock-free and reports persisted state including in-flight holds; it never takes
  `IUserUsageLock` — #8
- `IUsageEnforcer`'s and `UsageEnforcer`'s `<summary>` no longer describe a pure
  reserve/settle/release lifecycle — #1
- Controller supplies both objectId and `ClientIp` from `ICurrentUser`

## Acceptance criteria

- `GET /api/usage/quota` returns 200 with the three fields for an authenticated caller, and 401
  unauthenticated.
- A test proves a caller with no `CreditBalance` row gets `freeTokensUsed: 0` and the configured
  `freeQuotaMax`, and that **no row is created** by the call.
- A test proves each `ipConstraint` value is produced from the corresponding IP headroom state, and
  that no raw IP token number appears in the response payload.
- Remaining quota is computed by the same min-rule the enforcer applies at reserve time — no
  duplicate rule in the controller or DTO.
- `context.md` and `CLAUDE.md` document the endpoint alongside the billing routes.
- `dotnet test` passes.
