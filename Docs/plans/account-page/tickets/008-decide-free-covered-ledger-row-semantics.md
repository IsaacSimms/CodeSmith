---
id: 008
title: Decide free-covered ledger row semantics
type: grilling
status: open
blocked_by: []
---

## Question

**A spend row records a charge the user was never billed.** Surfaced while resolving ticket 001.

`SettleAsync` debits paid credits *prorated* to the non-free portion of the call
(`UsageEnforcer.cs:186-188`):

```csharp
var paidTokens = actualTokens - freeUsedThisCall;
if (paidTokens > 0 && actualTokens > 0)
    balance.PaidCreditsBalance -= chargeUsd * paidTokens / actualTokens;
```

…but the ledger row written immediately after stores the **undivided** amount
(`UsageEnforcer.cs:196`):

```csharp
CostUsd = chargeUsd,              // amount charged to the customer
```

So a call fully covered by free tokens debits **$0.00** and records a row claiming it cost
(say) $0.0042. `LedgerEntryResponse` exposes exactly that field as `AmountUsd` and carries no
token counts, so constraint 9's transaction list would show every free-tier user a running
column of dollar amounts they were never charged. Nothing has caught this because the ledger has
no frontend consumer — building this page is what makes it visible.

Resolve:

- What should a fully free-covered spend row display — `$0.00`, the notional cost with a "covered
  by free tokens" marker, or token counts instead of currency?
- What about a **partially** covered row, where some tokens were free and the remainder hit paid
  credits? This is the common boundary case as a grant runs out.
- Does `UsageLedgerEntry.CostUsd` change meaning, or does a new field carry the free/paid split?
  `CostUsd` is currently paired with `ProviderCostUsd` for margin reporting, so redefining it has
  an internal-reporting blast radius beyond the account page.
- Is the ledger table backfilled, or does the fix apply to new rows only? Existing rows cannot
  distinguish free-covered from paid.
- Does `LedgerEntryResponse` gain `InputTokens`/`OutputTokens`? The entity has them
  (`UsageLedgerEntry.cs:21-23`); the DTO deliberately omits them.
- Does the answer change what the All / Purchases / Usage filter chips mean in constraint 9 — is
  a $0.00 free-covered call a "Usage" row at all?

## Answer

<!-- Empty until resolved. -->
