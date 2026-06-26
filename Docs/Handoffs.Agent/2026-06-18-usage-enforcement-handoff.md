# Thread Handoff Document

> **Handoff Mode: Implementation**  
> **Receiving agent job: Resume and continue the next phase of SaaS cost protection work**

---

### 1. Thread Purpose (2–4 sentences)

This thread implemented the foundational data layer and usage enforcement seam for CodeSmith's transition to a public SaaS product. The primary goal was to protect the owner from unbounded LLM spend by enforcing per-user (Entra `objectId`) free monthly token quotas and (future) prepaid credits *before* any LLM call, while recording actual usage and costs *after*. All work followed the locked decisions from the provided SaaS decision docs and the grilled plan. The seams are now live and tested.

---

### 2. Stack & Environment

- Backend: .NET 8, ASP.NET Core Web API
- Data: Azure SQL Database (serverless, already provisioned with Managed Identity `mi-codesmith-backend-prod-centralus-001` having db_datareader/db_datawriter)
- AI/LLM: Anthropic (primary), OpenAI, xAI — accessed exclusively via `ILlmServiceFactory` + keyed services
- ORM: EF Core (new in this thread)
- Auth (skeleton): Entra External ID with dev bypass support
- Testing: xUnit + NSubstitute; full suite via `dotnet test` (no live server runs required for this phase)
- Platform: Windows, PowerShell, deployed to Azure Container Apps

---

### 3A. What Was Accomplished

- Extended `LlmResponse` (CodeSmith.Core/Models/LlmResponse.cs) with `OutputTokensUsed` and `Model`.
- Populated the new fields in all three LLM adapters (`AnthropicService.cs` / `AnthropicLlmService`, `OpenAiLlmService`, `XaiLlmService`) across all call types (problem gen, guidance, simulate, evaluate, test inputs, justification).
- Created Core entities and contracts (zero external dependencies):
  - `CreditBalance` and `UsageLedgerEntry` (Models/Usage/)
  - Repository interfaces: `ICreditBalanceRepository`, `IUsageLedgerRepository`
  - `IUsageEnforcer`, `ICurrentUser`, `ILlmPricing`
  - `InsufficientQuotaException`
- Implemented `ILlmPricing` (Infrastructure/Services/Usage/LlmPricing.cs) with static versioned rate table + `ComputeCostUsd` + conservative `EstimateUpperBoundCost` (highest-rate lean approach). TDD tests added and passing.
- Added `UsageOptions` (free monthly quota configurable).
- Built full usage enforcement seam:
  - `UsageEnforcer`: pre-check with upper-bound + free-first logic, monthly reset, actual record + deduct, strong consistency via repos.
  - Three decorators (`UsageEnforcing* LlmService`) that wrap the raw LLM implementations. Enforcement happens transparently; orchestrators (`TutoringService`, `ProblemGenerator`, PromptLab/SystemLab services) unchanged.
- Persistence layer:
  - `CodeSmithDbContext`, entity configurations, indexes (on objectId + timestamp), RowVersion for optimistic concurrency.
  - EF repository implementations.
- DI wiring updated in `ServiceCollectionExtensions.cs`: DbContext (via `ConnectionStrings:CodeSmithDb`), options, repos, pricing, enforcer, **keyed services now resolve through decorators**.
- API surface:
  - `HttpCurrentUser` (Api/Services/) supporting `X-Debug-User-Id` dev bypass header + Entra claim extraction (`oid` / objectidentifier / sub).
  - `InsufficientQuotaExceptionMapper` returning 402 Payment Required.
  - Registered `IHttpContextAccessor` + `ICurrentUser`.
  - Minimal auth/authorization skeleton in Program.cs (`UseAuthentication`/`UseAuthorization`).
  - `[Authorize]` applied *only* to LLM-spending actions:
    - SessionController: CreateSession, Chat
    - PromptLabController: StartChallenge, SubmitAttempt, Chat
    - SystemLabController: SubmitAttempt, Chat
- Verification strictly per plan: `dotnet build` + `dotnet test` (198 tests green). In-proc WebApplicationFactory + substitutes/SQLite used. No live API launches or manual HTTP against running server.
- All project coding conventions followed (block `// == Title == //` comments, no `/// <summary>` on members, Clean Architecture, UL terms, edit-in-place where possible, TDD on critical paths like pricing/enforcement).

---

### 4A. Current State

- The data layer (EF + Azure SQL ready) and usage enforcement seam are complete and wired.
- Every LLM call path (tutoring, PromptLab, SystemLab) now goes through protection decorators.
- Free monthly token quota enforcement (hard stop) + actual usage ledger recording are active (via dev header today).
- Paid credits path exists in schema and logic (debits after free exhausted).
- Auth is a minimal skeleton sufficient to surface `objectId` via `ICurrentUser`.
- Build and full test suite clean. No migrations applied yet (by design — later job).
- Session state remains in-memory. No frontend auth changes. No Stripe yet.

You are here: cost-protection seams for free tier + basic paid accounting are production-ready from a code perspective (pending full Entra + real SQL validation).

---

### 5. Key Decisions & Rationale

| Decision | Rationale |
|----------|-----------|
| Decorators as the enforcement seam around the three `*LlmService` interfaces | Preserves "LLM adapter behind all protection seams". Zero changes to orchestrators. True seam per UL (depth + locality). |
| Lean pre-check using highest rate × (est input + maxTokens) | Avoids duplicating model selection logic inside decorators. Safe conservative bound; actual cost always used for record/deduct. |
| Free quota (tokens) tracked separately from PaidCreditsBalance (cost/USD) | Matches locked prepaid + free-tier model. Enables accurate future Stripe pass-through without conflating grants vs. purchases. Free-first debit policy chosen. |
| `ICurrentUser` as the identity seam (dev header + Entra claims) | Single place to obtain stable `objectId`. Enables hybrid dev/prod without changing enforcer or decorators. |
| Static rate table in `ILlmPricing` (Core interface, Infra impl) | Testable, versioned, no DB/config dependency for v1. Matches "pricing table lives in Core so it is testable". |
| 402 + dedicated mapper for `InsufficientQuotaException` | Clear client signal. Follows existing `IExceptionMapper` + `AppExceptionHandler` pattern exactly. |
| `[Authorize]` only on spending actions + minimal auth skeleton | Enough for `objectId` without full Entra setup blocking the seam work. Per user direction ("accomplish these specific seams then be done for now"). |
| Verification limited to build + `dotnet test` (no live runs) | Explicit user instruction during this thread. |

---

### 6. Blockers & Open Questions

None blocking for the seams themselves. All load-bearing work for data + enforcement is done and green.

---

### 7. Next Steps (Ordered)

1. **Pressure-test the current seams** in a fresh session if needed: exercise via dev header + unit/integration tests (WebApplicationFactory), confirm decorators are always in the call path, verify free quota exhaustion returns 402 and never reaches LLM SDKs, test free-first + reset logic.

2. **Add Stripe prepaid credits flow** (highest priority per original decisions):
   - Stripe Checkout for credit packs.
   - Webhook (secure, signature verified) that credits `PaidCreditsBalance`.
   - Separate billing module (do **not** touch `IUsageEnforcer`).

3. **Full Entra External ID wiring**:
   - Proper `AddMicrosoftIdentityWebApi` + policies in Program.cs.
   - Frontend token acquisition (MSAL) or documented manual token flow for testing.
   - Remove or harden the `X-Debug-User-Id` bypass for non-dev.

4. **Migrate / apply the initial EF migration** against the provisioned Azure SQL (using Managed Identity) as a later job / one-time operation. Do not add automatic `Migrate()`.

5. **Per-user rate limiting** (partition by `objectId` instead of or in addition to IP) once identity is solid.

6. **Optional polish**: expose basic usage summary endpoint (read-only from ledger), improve error messages for clients, add basic owner-visible spend telemetry (without exposing full global costs).

---

### 8. Must-Knows for the New Thread

- **Seam placement is non-negotiable**: All LLM calls must go through the decorators. Do not call `AnthropicLlmService` / etc. directly from controllers or orchestrators.
- **ICurrentUser is the source of truth** for `objectId`. Never read claims or headers yourself in enforcement or business logic.
- **Free quota is token-based hard stop**; paid is cost-based. Pre-check uses upper-bound estimate; post-call always uses actuals + exact model from `LlmResponse`.
- **Strong consistency matters**: Balance checks + deducts must prevent race conditions (RowVersion + transactional thinking in enforcer/repos).
- **Clean Architecture preserved**: Core has no EF, no HTTP, no pricing rates (only interface). Everything new respects this.
- **Ubiquitous Language terms** (from project Claude.md): Module, Interface, Seam, Adapter, Depth, Leverage, Locality. Use them when discussing the usage enforcer and decorators.
- **Verification rule from this thread**: Build + full `dotnet test`. Do not require or implement live server + manual HTTP flows unless explicitly asked.
- **Infra is already ready**: Azure SQL + MI + Key Vault exist. Connection key is `CodeSmithDb`.
- **Next big money/cost feature is Stripe**, not more LLM protection or telemetry.

---

### 9. Relevant Artifacts

- `CodeSmith.Core/Models/Usage/CreditBalance.cs` + `UsageLedgerEntry.cs` — entities
- `CodeSmith.Core/Interfaces/IUsageEnforcer.cs`, `ICurrentUser.cs`, `ILlmPricing.cs` + repo interfaces
- `CodeSmith.Infrastructure/Services/Usage/LlmPricing.cs`, `UsageEnforcer.cs`, `Decorators/*`
- `CodeSmith.Infrastructure/Persistence/CodeSmithDbContext.cs` + Repositories/
- `CodeSmith.Infrastructure/Configuration/UsageOptions.cs`
- `CodeSmith.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs` — critical wiring
- `CodeSmith.Api/Services/HttpCurrentUser.cs`
- `CodeSmith.Api/Middleware/ExceptionMappers/InsufficientQuotaExceptionMapper.cs`
- `CodeSmith.Api/Program.cs` — auth skeleton + registrations
- `CodeSmith.Core/Exceptions/InsufficientQuotaException.cs`
- Updated: `LlmResponse.cs`, three LLM service files, three controllers (Authorize attributes)
- Session plan artifact (for reference): the detailed plan used to drive this work
- Original sources: `~/Downloads/codesmith-saas-decisions-next-steps-2026-06-17.md` etc. (locked decisions)

---

**Paste into new thread:**

"Picking up from a previous session. Here's the handoff: [paste the entire document above]

Confirm you have context and flag anything unclear before we continue. The immediate goal is to pick the next highest-value increment (Stripe credits is the obvious one) while preserving the protection seams we just built."