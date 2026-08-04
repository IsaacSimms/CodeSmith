---
id: 007
title: Design the nav-dropdown balance summary
type: grilling
status: closed
blocked_by: [002]
---

## Question

Settled constraint 1 puts a "balance summary" in the top-nav dropdown alongside "Account" and
"Sign out". Nothing about that summary is specified, and **no ticket owned it** — ticket 005
covers the account page body, not the nav.

There is no credits or quota UI anywhere in the app today: grepping `CodeSmith.Web/src` for
`balance` returns nothing, and `/api/billing/balance` and `/api/billing/ledger` ship with zero
frontend consumers. This is the app's first at-a-glance money surface.

Resolve:

- What does the summary show — paid credits only, free tokens only, or both? Ticket 001 settled
  that the two cannot merge into one number (tokens vs USD, no conversion per constraint 6), and
  a dropdown has far less room than a page section.
- What does it show once the free grant is exhausted? Ticket 001 chose a muted collapsed line on
  the page; the dropdown may not have room for even that.
- Which queries back it? **Cache-key sharing is no longer open for the quota query**:
  [Define the free-quota endpoint contract](002-define-free-quota-endpoint-contract.md) decision 8
  makes the quota read lock-free, so it reports in-flight reservation holds and self-corrects only
  when the SPA invalidates on turn completion. The dropdown is visible *during* a conversation, so
  it must share the invalidated key or it will sit on an inflated number mid-chat. What remains open
  is the balance query, where no such coupling exists.
- When does it fetch? The dropdown is closed most of the time. Fetch on mount of `Layout`, on
  dropdown open, or on hover?
- What renders while loading, and what renders on error? A money figure that flickers or shows a
  stale value is worse than one that shows a skeleton.
- Does it render at all for an unauthenticated user? `AuthControls.tsx:8` returns `null` when
  `isMsalConfigured()` is false, so in local dev the dropdown does not exist — see constraint 8.
- Is the summary itself a link to `/account#credits`, or does it sit above the existing
  "Account" item as passive text?

## Answer

**A lifecycle mode switch in passive nav chrome: free remaining while the account grant has
headroom, paid USD only after that grant is exhausted.** The page (005) owns the full wallet;
the dropdown answers one glance question at a time and never merges units.

### Contract

Authenticated `AuthControls` becomes the constraint-1 dropdown (email/label toggle, reuse
outside-click/Escape). Menu order:

1. **Balance summary** — passive text (not a link, not a menuitem action)
2. **Account** → `/account`
3. **Sign out**

Mode and copy:

| Condition | Summary |
|-----------|---------|
| Free grant active (`freeTokensUsed < freeQuotaMax`) | One line: remaining only, e.g. `12,400 free tokens` |
| Free grant exhausted (`freeTokensUsed >= freeQuotaMax`) | One line: formatted paid USD, including `$0.00`, e.g. `$12.40 credits` / `$0.00 credits` |
| Loading (mode or active figure unknown) | Stable slot, muted placeholder (`—` / `…`) |
| Error on the query required for the active mode | **Omit** the summary row; Account + Sign out still render |

Queries (mounted when authenticated, shared with Account):

```
['usage', 'quota']     → GET /api/usage/quota
['billing', 'balance'] → GET /api/billing/balance
```

Prefetch both at Layout (or equivalent authenticated shell) so the first open is warm and
mid-chat invalidation applies with the menu closed. Invalidate **both** keys on metered turn
settle and on top-up detection ([006](006-decide-topup-completion-signal.md)).

### Decisions

| # | Decision | Reasoning |
|---|----------|-----------|
| 1 | **Lifecycle mode switch**, not a permanent two-line wallet | Tokens and USD cannot merge (001 / constraint 6). A dropdown has no room for the page's paired wallet row (005). Over an account's life the user is in a free era then a paid era; the nav shows one era at a time |
| 2 | While free is active: **free line only** (paid hidden) | One glance number. Paid balance after a mid-trial top-up lives on Account until free is spent — accepted cost of keeping the chrome thin |
| 3 | Free copy is **remaining only** (`max - used`), not used/max and not a mini bar | Status chip, not a second `UsageBar`. Full used/max + fill lives on the account free-quota card |
| 4 | Exhausted = **account grant only** (`freeTokensUsed >= freeQuotaMax`). **IP is not in the dropdown** | Matches the one-time grant product story. IP binding honesty is already owned by the page notice (001 #7); the menu has no room for a second notice. Accepted cost: an IP-exhausted user can still see free remaining they cannot spend free |
| 5 | Summary is **passive text** above real Account + Sign out items | Constraint 1 lists three things; status is not navigation. Avoids a dual-purpose hit target and keeps Account the one path into the page |
| 6 | **Prefetch when authenticated** (both queries), not fetch-on-open | Instant open; 002 #8's mid-chat correction only helps if the quota query stays mounted while the user chats with the menu closed |
| 7 | Shared TanStack keys with Account: `['usage','quota']`, `['billing','balance']` | One cache — dropdown and page cannot disagree. 002 already required shared quota invalidation for the lock-free overstatement |
| 8 | Invalidate **both** keys on every metered **turn settle**, and both on **top-up success** (006) — *amended by [ticket 008](008-decide-free-covered-ledger-row-semantics.md) #12: the set gains `['billing','ledger']`* | One client rule, no free/paid branching in the invalidator. Balance has no in-flight hold coupling like quota, but paid spends still move USD; always invalidating both is cheaper than a wrong nav number |
| 9 | Loading → muted placeholder in a **stable slot**; never invent `$0.00` or a token count | A flicker or false zero is worse than a dash. App has no skeleton language (005) |
| 10 | Error → **hide the summary row** entirely | Failed glance is optional; Account is the recovery surface. No "Balance unavailable" chrome in a three-row menu |
| 11 | Paid mode always shows **formatted USD**, including **`$0.00`** — *amended by [ticket 008](008-decide-free-covered-ledger-row-semantics.md) #11: `$0.00` only for a true zero; a spendable sub-cent balance renders `< $0.01`* | Uniform with 005's zero-state philosophy; zero is a real fact, not an empty state that hides the money surface constraint 1 put in the nav |

### Derived / implement notes (not re-grilled)

- Clamp free remaining at `≥ 0` if reservation holds make `used` temporarily overshoot `max` (002 #8).
- Unauthenticated users never see this summary; MSAL-off dev still has no `AuthControls` (constraint 8's dev nav entry is separate).
- Exact string templates and USD fraction digits are presentation defaults for implement, not product branches.

### Codebase facts that shaped this

- **Authenticated nav is not a dropdown yet.** `AuthControls.tsx:57-72` is label + Sign out button;
  only the sign-in chooser uses the outside-click/Escape menu (`:87-114`). Constraint 1 requires
  converting the authenticated path — this ticket designs the money row that lands inside it.
- **No billing consumers in the SPA.** Balance/ledger endpoints ship without frontend callers;
  this is the first surface that forces shared query keys and turn-settle invalidation.
- **Quota mid-turn overstatement** is real and conservative (002 #8). A dropdown that opened only
  on demand and unmounted its query would not self-correct during chat.

### Consequences for the map

- **Unblocks nothing new on the frontier.** 008 / 009 / 010 remain independent grilling tickets.
- **Constrains implement work for AuthControls + Account data hooks:** shared keys, Layout-level
  prefetch when authenticated, dual invalidation on turn settle and on 006 top-up detection.
- **Does not amend** constraints 1, 2, 6, or 8 — it fills the gap constraint 1 left open.
- No new tickets. IP-in-nav and dual-line free+paid were considered and rejected inside this ticket.
