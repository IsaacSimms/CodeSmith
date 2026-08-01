---
id: 002
title: Define the free-quota endpoint contract
type: grilling
status: open
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

<!-- Empty until resolved. -->
