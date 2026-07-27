# Thread Handoff Document

> **Handoff Mode: Implementation**
> **Receiving agent job: Verify/test the delivered Stripe prepaid-credits billing module end-to-end**

---

### 1. Thread Purpose

A prior grill-me session corrected a Stripe billing design (seven flawed assumptions in the original handoff), then this thread **implemented** the module across four phases. Backend is complete: `dotnet build` clean (0 warnings), `dotnet test` green at **275/275**. Nothing is committed — all changes sit in the working tree for review. The receiving agent's job is to **test this implementation**: confirm the automated suite, then drive the Stripe Checkout → webhook → credit flow with the Stripe CLI, since unit tests deliberately do not exercise the real Stripe API or a live SQL database.

---

### 2. Stack & Environment

- **Backend:** .NET 8, ASP.NET Core Web API (Clean Architecture: `CodeSmith.Core` / `CodeSmith.Infrastructure` / `CodeSmith.Api`).
- **Payments:** `Stripe.net 52.*` (added this thread), test mode.
- **Data:** Azure SQL via EF Core. Local dev needs a SQL connection string under `ConnectionStrings:CodeSmithDb` (note: `appsettings.Development.json` currently only has a `sql-codesmith-prod-...` named string — see Must-Knows §8).
- **Auth:** Entra External ID Bearer; in Development an allow-listed `X-Debug-User-Id` header satisfies `[Authorize]` (debug objectId `11111111-1111-1111-1111-111111111111` is already allow-listed in `appsettings.Development.json`).
- **Tests:** xUnit + NSubstitute; EF InMemory (`Microsoft.EntityFrameworkCore.InMemory`, added this thread to `CodeSmith.Tests`).
- **Ports:** API HTTP `5175`, HTTPS `7111`; frontend `5173` (not involved — no frontend in this increment).
- **Solution file:** `CodeSmith.Api/CodeSmith.Api.sln` (note the non-root location).

---

### 3A. What Was Accomplished

**Phase 0 — Package & config**
- `Stripe.net 52.*` → `CodeSmith.Infrastructure.csproj`.
- `StripeOptions` (`Configuration/StripeOptions.cs`): `SecretKey`, `WebhookSecret`, `string[] PriceIds`, `SuccessUrl`, `CancelUrl`. Bound in `ServiceCollectionExtensions`.
- `appsettings.json` → `Stripe` section with the three test Price IDs + placeholder success/cancel URLs. `appsettings.Development.json` → `Stripe.WebhookSecret` set; `Stripe.SecretKey` **left blank**.

**Phase 1 — Data layer** (`EfStripeCreditStoreTests`, 4 tests)
- `LedgerEntryType {Spend=0, TopUp=1}` enum. `UsageLedgerEntry` gained `Type`; `Provider`/`Model` made nullable.
- `ProcessedStripeEvent` entity (PK = Stripe event id) + `DbSet` + config.
- `ICreditBalanceRepository.GetOrCreateAsync(objectId, freeQuotaMax)` + shared `CreditBalance.CreateNew` factory; `UsageEnforcer` refactored to use it.
- `IStripeCreditStore` / `EfStripeCreditStore` — deep seam: dedup + credit + TopUp ledger row in **one `SaveChangesAsync`**, with `DbUpdateConcurrencyException` retry and duplicate-event replay handling.
- Migration `20260707051657_AddStripeBilling` (+ snapshot updated).

**Phase 2 — Service + Stripe seam** (`StripeBillingServiceTests`, 9 tests)
- `IBillingService` (Core, no Stripe types) + `WebhookResult {Credited, AlreadyProcessed, Ignored}`.
- `InvalidPriceException`, `WebhookSignatureException` (Core).
- `IStripeEventReader` / `StripeEventReader` (Infrastructure internal seam over `EventUtility.ConstructEvent`).
- `StripeBillingService`: allow-list-validated checkout, idempotent webhook (USD guard, `objectId` metadata guard, positive-amount guard, cents→USD), balance/ledger reads. `objectId` only from `ICurrentUser`.

**Phase 3 — API** (`BillingControllerTests`, 5 tests)
- `BillingController` (`api/billing`): `checkout`/`balance`/`ledger` `[Authorize]`; `webhook` `[AllowAnonymous]`, reads **raw body** (no `[FromBody]`).
- DTOs (`DTOs/Billing/`): `CheckoutRequest`, `CheckoutResponse`, `BalanceResponse`, `LedgerEntryResponse` — the last omits `ProviderCostUsd` and `RowVersion`.
- `InvalidPriceExceptionMapper` + `WebhookSignatureExceptionMapper` (both → 400), registered in `Program.cs`.

---

### 4A. Current State

- **Complete and green.** `dotnet test CodeSmith.Tests/CodeSmith.Tests.csproj` → 275 passed, 0 failed. `dotnet build CodeSmith.Api/CodeSmith.Api.sln` → 0 warnings, 0 errors.
- **Uncommitted.** All work is in the working tree (branch `master`); no commit made.
- **Not yet exercised:** the real Stripe API (checkout creation), a live SQL DB, the migration applied to a real database, and actual signature verification against a Stripe-signed payload. These are exactly what manual/CLI testing must cover.

---

### 5. Key Decisions & Rationale

| Decision | Rationale |
|----------|-----------|
| Idempotency via `ProcessedStripeEvent` table, insert inside the credit transaction | Stripe delivers at-least-once; additive `balance += amount` is not idempotent. Overrode the original handoff's false "no new migrations / trust Stripe." |
| Credit lives in a deep `IStripeCreditStore`, not scattered across repos | Dedup + credit + ledger must commit atomically in one `SaveChangesAsync`. Store loads the balance tracked and retries on concurrency conflict — catches cross-context races with the enforcer natively. |
| `EfCreditBalanceRepository.SaveAsync` left unchanged | The plan called for a RowVersion rewrite only if the webhook credited through `SaveAsync`. It doesn't (store owns concurrency), so the rewrite was unnecessary. `SaveAsync` is correct for the enforcer's tracked path. |
| `LedgerEntryType` discriminator; `Spend=0` | A top-up isn't an LLM call. `Spend=0` keeps all existing enforcer writes correct with zero enforcer changes. |
| `IStripeEventReader` internal seam | Makes the webhook handler unit-testable via substitute — no need to mint real HMAC signatures. Kept in Infrastructure because it returns a Stripe type; Core stays processor-agnostic. |
| Webhook HTTP contract: 400 bad sig / 200 processed-or-dup-or-ignored / 500 transient | 500 makes Stripe retry a transient DB failure; 200 on ignored (e.g. non-USD) stops pointless retries. Unmapped exceptions → 500 via `AppExceptionHandler`. |
| `/ledger` DTO omits `ProviderCostUsd` | That field is raw provider cost — serializing it leaks the markup/margin on every line. |
| Checkout returns hosted `session.Url` (redirect mode) | Testable by pasting the URL in a browser; needs no frontend. |
| `freeTokensRemaining` deferred on `/balance` | Would duplicate the enforcer's private 48h-window rule. `BalanceResponse` is paid-credits-only. |

---

### 6. Blockers & Open Questions

- **`Stripe:SecretKey` is blank.** Checkout creation (`POST /api/billing/checkout`) will fail until a `sk_test_...` is supplied via user-secrets or `appsettings.Development.json`. The webhook path does **not** need it (only `WebhookSecret`, already set).
- **Local SQL connection string name mismatch (inferred):** `ServiceCollectionExtensions` reads `configuration.GetConnectionString("CodeSmithDb")`, but `appsettings.Development.json` defines `ConnectionStrings:sql-codesmith-prod-centralus-001`. Applying the migration / running the app locally likely needs a `CodeSmithDb` connection string. Confirm before the live test.
- No frontend, no Customer Portal, no subscriptions/refunds — all explicitly out of scope.

---

### 7. Next Steps (Ordered)

1. **Confirm the automated suite:** `dotnet test CodeSmith.Tests/CodeSmith.Tests.csproj` → expect 275 passing.
2. **Set `Stripe:SecretKey`** (`sk_test_...`) in user-secrets or `appsettings.Development.json`, and ensure a working `CodeSmithDb` connection string exists locally.
3. **Apply the migration:** `dotnet ef database update --startup-project CodeSmith.Api/CodeSmith.Api.csproj` (from `CodeSmith.Infrastructure`). Confirm `ProcessedStripeEvents` table + `UsageLedgerEntries.Type` column exist.
4. **Run the API** (`cd CodeSmith.Api && dotnet run`) and start `stripe listen --forward-to https://localhost:5175/api/billing/webhook`; capture the CLI's webhook secret if it differs from the configured one.
5. **Create a checkout:** `POST /api/billing/checkout` with `{ "priceId": "price_1Tnt9nRzQXBJQm3BK0llW9f7" }` ($5) plus the debug auth header. Confirm a Stripe URL is returned; complete the test purchase.
6. **Verify the credit:** confirm `PaidCreditsBalance` rose by $5, a `TopUp` `UsageLedgerEntry` appeared, and a `ProcessedStripeEvent` row was written. Hit `GET /api/billing/balance` and `GET /api/billing/ledger`.
7. **Idempotency test:** `stripe events resend <evt_id>` (or trigger a duplicate) — confirm the balance does **not** change and no second ledger row appears.
8. **Negative tests:** tamper with the `Stripe-Signature` header → expect **400**; `POST /checkout` with an unknown priceId → expect **400**; call authorized endpoints without auth → expect **401**.
9. **Free-quota → paid transition:** exhaust free quota, then confirm an LLM call succeeds against the newly-added paid credits.

---

### 8. Must-Knows for the New Thread

- **Verification rule:** the implementer worked under "`dotnet test` only, no running server required for delivery." Live-server/CLI testing is *this* agent's job — don't treat its absence as incomplete work.
- **Seam is non-negotiable and currently intact:** billing has **zero** references to `IUsageEnforcer`, `IUserUsageLock`, or `ILlmService` (verified by grep). Do not introduce any. Enforcement remains the only thing that debits balances.
- **`objectId` only from `ICurrentUser`** — never read claims/headers directly in billing.
- **Raw body is mandatory** for the webhook; signature verification hashes exact bytes. Do not add `[FromBody]` to the webhook action.
- **Duplicate-event handling is by design:** the store treats a re-seen event id (or PK-collision race) as `AlreadyProcessed` and makes no change — the correct, safe behavior.
- **User conventions:** block `// == Title == //` comments; `/// <summary>` only at type level; TDD; direct pushback expected, no affirmations; reviews every line.
- The three test Price IDs: `price_1TntDORzQXBJQm3BtbhkfobM` ($25), `price_1TntCSRzQXBJQm3BDDwBF5Je` ($10), `price_1Tnt9nRzQXBJQm3BK0llW9f7` ($5). Configured webhook secret:

---

### 9. Relevant Artifacts

| File | What it does | State |
|------|--------------|-------|
| `CodeSmith.Infrastructure/Billing/StripeBillingService.cs` | Checkout + webhook + reads | Complete |
| `CodeSmith.Infrastructure/Billing/{IStripeEventReader,StripeEventReader}.cs` | Signature-verify seam | Complete |
| `CodeSmith.Infrastructure/Persistence/Repositories/EfStripeCreditStore.cs` | Atomic idempotent credit | Complete |
| `CodeSmith.Core/Interfaces/{IBillingService,IStripeCreditStore}.cs` | Core seams | Complete |
| `CodeSmith.Core/Models/Usage/{ProcessedStripeEvent,CreditBalance,UsageLedgerEntry}.cs` | Entities | Complete |
| `CodeSmith.Api/Controllers/BillingController.cs` | 4 endpoints | Complete |
| `CodeSmith.Api/DTOs/Billing/*.cs` | Request/response DTOs | Complete |
| `CodeSmith.Api/Middleware/ExceptionMappers/{InvalidPrice,WebhookSignature}ExceptionMapper.cs` | 400 mappers | Complete |
| `CodeSmith.Infrastructure/Migrations/20260707051657_AddStripeBilling.cs` | Schema change | Generated, not applied to a real DB |
| `CodeSmith.Tests/Infrastructure/Billing/*.cs`, `CodeSmith.Tests/Api/BillingControllerTests.cs` | 18 new tests | Green |

---

**Paste into new thread:**

> "Picking up from a previous session. Here's the handoff: [paste document]
> Confirm you have context and flag anything unclear before we continue."
