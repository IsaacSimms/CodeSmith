---
id: 008
title: Decide free-covered ledger row semantics
type: grilling
status: closed
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

**`CostUsd` starts telling the truth.** On a Spend row it becomes the amount actually debited —
`$0` when free tokens covered the call, the prorated amount when they covered part of it — and a
new `FreeTokensCovered` column keeps the free portion auditable. The transaction list renders a
fully covered row as **"Free"** rather than as a dollar figure.

The framing that did the most work here was not on the question list. This is not a presentation
bug with a correct number behind it; it is **one wrong number with two victims**. `CostUsd` is
`raw × PaidMarkupMultiplier`, so on a free-covered call it claims revenue that never arrived while
`ProviderCostUsd` records money we really spent — meaning `CostUsd − ProviderCostUsd` currently
reports the free tier as **profitable**. Fixing the user-facing lie fixes the internal one.

A read-time fix was ruled out before the interview started: the row carries `InputTokens`,
`OutputTokens`, `CostUsd`, and `ProviderCostUsd`, and **nothing** about free coverage. Given a row,
the split is not derivable. Whatever we do lands at write time.

### Decisions

| # | Decision | Reasoning |
|---|----------|-----------|
| 1 | **`UsageLedgerEntry.CostUsd` on a Spend row changes meaning to the actual amount debited** — `$0` when fully free-covered, the prorated amount when partial. Fix lands in `SettleAsync` | The alternative — keeping `CostUsd` notional and adding a `PaidAmountUsd` beside it — leaves two amount concepts to keep straight forever, and every future reader has to know which is which. One column, one meaning |
| 2 | **New `FreeTokensCovered` (`int?`) column** on `UsageLedgerEntry` | Keeps the free portion auditable once `CostUsd` stops carrying it, and makes the free tier's true cost recoverable. Nullable so "written before this column existed" stays distinguishable from "provably zero free coverage" — same convention `ProviderCostUsd` already uses (`UsageLedgerEntry.cs:27`) |
| 3 | **The debited decimal and the stored decimal are one computed value** — `SettleAsync` computes the prorated charge once into a local, debits it, and writes that same value to the row | Today `:188` computes the debit inline and `:197` stores `chargeUsd` separately. Both columns are `HasPrecision(18, 6)`. Computing twice means ledger sums never reconcile against `PaidCreditsBalance` — which is the reconciliation this ticket exists to enable |
| 4 | **A fully free-covered row renders "Free"** in the amount slot, no currency | A real call costs ~$0.0042, so a genuinely charged row already rounds toward `$0.00`. Rendering free rows as `$0.00` too would make "we didn't charge you" indistinguishable from "we charged a fraction of a cent". Accepted cost: those rows break strict `tabular-nums` alignment |
| 5 | **A partially covered row renders as an ordinary paid row** — charged amount only, no marker | Free cover is `Math.Min(objectFreeRem, ipRem, totalTokens)` (`:283`) and the grant never resets (ticket 001), so `objectFreeRem` only decreases. Each account produces **exactly one** partial row in its entire lifetime — two at the outside, if the IP cap binds separately. A third row state for a once-ever row does not earn its code. `FreeTokensCovered` still records it |
| 6 | **Spend rows format at 4dp; TopUp rows at 2dp** | `Type` already partitions them, so each kind aligns cleanly within itself. Uniform 2dp would collapse nearly every usage row to `< $0.01` and destroy the resolution constraint 9 exists to provide; uniform 4dp makes a purchase read `$10.0000` on the one row where the number matters most |
| 7 | **No backfill.** Pre-fix rows keep their notional `CostUsd`, with `FreeTokensCovered` null | `EfUsageLedgerRepository.cs:27` orders `TimestampUtc` descending with `take` clamped to 100 — the ledger is a **recent-N window, not a full history**. Pre-fix rows age out on their own. A data migration would buy permanent schema-change risk to fix something that expires by itself. A provably-correct partial backfill *was* available (any `objectId` with no `TopUp` row never had credits, so its Spend rows were necessarily free-covered) and was rejected on this reasoning, not for lack of a safe method |
| 8 | **`LedgerEntryResponse` gains `isFreeCovered: bool`** and nothing else — no `InputTokens`/`OutputTokens` | The server owns the billing rule, matching ticket 002's precedent that the quota rule is never re-derived outside the module that enforces it. Deriving from `amountUsd === 0` would put a billing rule in a `.tsx` file and mislabel any row that legitimately stores zero. Token counts stay omitted — decision 5 leaves them with no consumer. Old rows return `false` and render as paid amounts, which is exactly the drift decision 7 accepts |
| 9 | **Filter chips map 1:1 to `LedgerEntryType`** — a Free row is a Usage row | Usage means an LLM call happened; the amount is irrelevant. Any other answer hands the free-tier user — the person most wanting to see their activity — an empty Usage list beside a full All list |
| 10 | **Ship after ticket 009**, as a separate slice | Both edit `SettleAsync` and both rewrite parts of `UsageEnforcerTests`. 009 deletes `WindowActive` (`:262`) and its call sites (`:72`, `:176`), leaving a smaller method; the rebase cost then falls on this ticket, the lighter of the two. Bundling them would put a user-facing correctness fix and a product-mechanic removal in one unbisectable commit |
| 11 | **USD balances render 2dp, but a spendable sub-cent balance renders `< $0.01`, never `$0.00`.** `$0.00` is reserved for a true zero. Ledger Spend rows keep 4dp per decision 6 | `PaidCreditsBalance` is `HasPrecision(18, 6)` (`CodeSmithDbContext.cs:25`) and a call costs ~$0.0042, so a rounded `$0.00` can be a lie about a balance the user can still spend — the same sub-cent collision decision 4 solved for rows, reappearing on the balance. Preserves ticket 007 #11's "zero is a real fact"; it only stops non-zero from impersonating zero. **Amends 007 #11** |
| 12 | **`['billing','ledger']` joins the turn-settle invalidation set**, alongside `['usage','quota']` and `['billing','balance']`. Implement note for the account data hooks, not a product branch | A metered turn settle appends a ledger row in the same `PersistAsync` unit of work (`:206`). 007 #8 listed only the two keys the dropdown reads; the account history section reads a third, so a user on `/account` who runs a turn elsewhere would sit on a stale list. **Amends 007 #8** |

### Codebase facts that shaped this

- **Nothing reads `ProviderCostUsd`.** It is written at `UsageEnforcer.cs:198`, nulled at
  `EfStripeCreditStore.cs:54`, given precision at `CodeSmithDbContext.cs:35`, and asserted in two
  tests. "Margin reporting" is a documented intent (`context.md:237`), not a running query — so the
  ticket's warning about an internal-reporting blast radius was larger than the reality. There is no
  report to break, only a definition to correct.
- **The ledger is a recent-N window.** `GetRecentAsync` (`EfUsageLedgerRepository.cs:23-29`) orders
  descending on `TimestampUtc` and takes at most 100, clamped again at `BillingController.cs:74`.
  This is what makes decision 7 safe rather than merely cheap.
- **Sub-cent amounts are the norm, not an edge case.** `CostUsd`, `ProviderCostUsd`, and
  `PaidCreditsBalance` are all `HasPrecision(18, 6)` while a real call lands around $0.0042. Any 2dp
  money rendering in this app is a rounding decision about the common case, which is why decisions 6
  and 11 exist at all.
- **The partial row is a lifecycle boundary, not a state.** Ticket 007 #1 independently framed an
  account as "a free era then a paid era"; decision 5's once-per-account partial row is the single
  transaction that sits between them. The two tickets agreed without coordination.

### Consequences for the map

- **Amends [ticket 007](007-design-nav-dropdown-balance-summary.md) twice** — #11 (`$0.00 credits`
  now only for a true zero) and #8 (invalidation set gains the ledger key). 007 stays closed; both
  amendments are noted in its file.
- **Reaches [ticket 005](005-choose-account-page-layout.md)** without contradicting it. 005 #9 fixed
  the ledger row at three visible things and never specified a money format; decisions 6 and 11 fill
  that in. The credits card renders the same balance as the nav and follows decision 11.
- **Ordered behind [ticket 009](009-remove-free-token-time-window.md)** for implementation — see
  decision 10. Does not block 009's grilling.
- **Doc drift to fix when the code lands:** `UsageLedgerEntry.cs:25`, `context.md:237`, and
  `LedgerEntryResponse.cs:7-9` all describe `CostUsd` in terms decision 1 invalidates.
- No new tickets. Every branch this ticket opened closed inside it.
