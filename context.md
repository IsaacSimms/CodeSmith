# CodeSmith — Context & Architecture Reference

CodeSmith is an AI-powered practice tool for technical interviews. It hosts three independent practice surfaces — **Tutoring** (coding problems with a Socratic pair-programmer), **Prompt Lab** (prompt-engineering challenges scored against a rubric), and **System Lab** (system-design justification scenarios) — over a shared, provider-agnostic LLM layer. Every LLM call is metered against a per-user free quota and paid credit balance so the SaaS cannot be run at a loss.

This document is the ground-truth architectural reference. It reflects the repo as of 2026-07-15 (reviewed 2026-07-15). Keep the Seams table, API Reference, subsystem sections, and the [Ubiquitous Language](#ubiquitous-language) glossary updated as the architecture evolves.

> **Vocabulary note.** This project uses a deliberate architecture vocabulary — **Module, Interface, Implementation, Depth, Seam, Adapter, Leverage, Locality**. Definitions are in the [Ubiquitous Language](#ubiquitous-language) section at the end. Use these terms exactly; do not substitute "component / service / boundary."

---

## Stack

| Layer          | Technology                                      |
|----------------|-------------------------------------------------|
| Backend        | .NET 8, ASP.NET Core Web API                    |
| LLM providers  | Anthropic SDK; OpenAI SDK (also drives xAI/Grok via OpenAI-compatible endpoint) |
| Persistence    | EF Core + SQL Server (usage/credits); in-memory session stores |
| Code execution | Piston (Docker sandbox, default) or LocalProcess (dev fallback) |
| Telemetry      | OpenTelemetry → Azure Monitor / Application Insights (active only when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set) |
| Frontend       | React 19, TypeScript, Vite 6                    |
| Styling        | Tailwind CSS v4 (VS Code Dark Modern palette)   |
| Data fetching  | TanStack Query v5                               |
| Routing        | React Router v6                                  |
| Backend tests  | xUnit, NSubstitute                              |
| Frontend tests | Vitest, React Testing Library                   |
| E2E            | Playwright                                       |

---

## Project Structure

```
CodeSmith.Core/            — Domain models, enums, exceptions, interfaces (no I/O, no SDKs)
  Enums/                   — AiProvider, ModelTier, Difficulty, Language, MessageRole, GuidanceMode,
                             EvaluationMode, ChallengeCategory, SystemLabCategory, PromptFieldType, LedgerEntryType
  Exceptions/              — Domain exceptions (each maps to one HTTP status, see below)
  Interfaces/              — All seams live here (ILlm*, I*Service, ISessionStore, IUsage*, etc.)
  Models/                  — ChatMessage, LlmResponse, ProblemSession, CodeExecutionResult,
                             PromptLab/*, SystemLab/*, Usage/*

CodeSmith.Infrastructure/  — Implementations of Core interfaces; the only project that touches SDKs/EF/HTTP
  Billing/                 — StripeBillingService + IStripeEventReader/StripeEventReader (Stripe seam)
  Configuration/           — Options classes (Anthropic, OpenAi, Xai, Ai, CodeExecution, Usage, Stripe)
  DependencyInjection/     — ServiceCollectionExtensions.AddCodeSmithInfrastructure (composition root)
  Persistence/             — CodeSmithDbContext + EF repositories (credit balance, usage ledger, IP free-usage aggregate, Stripe credit store)
  Services/                — LLM adapters, generators, lab orchestrators, session stores
    PromptLab/             — ChallengeCatalog, PromptSimulator, PromptEvaluator, TestInputGenerator, PromptLabService
    SystemLab/             — ScenarioCatalog, SystemLabEvaluator, SystemLabService
    Piston/                — Sandboxed code-execution adapter + runtime resolver
    Usage/                 — UsageEnforcer, LlmPricing, UserUsageLock, NoopCurrentUser, Decorators/

CodeSmith.Api/             — ASP.NET Core host (HTTPS 7111, HTTP 5175)
  Controllers/             — SessionController, PromptLabController, SystemLabController, BillingController
  DTOs/                    — Request/response shapes per surface (PromptLab/, SystemLab/, Billing/)
  Middleware/              — AppExceptionHandler (declarative exception→status table); RequestLoggingMiddleware
  Services/                — HttpCurrentUser (resolves Entra objectId, dev bypass)

CodeSmith.CLI/             — Command-line client over the API (ApiClient)

CodeSmith.Tests/           — Backend xUnit tests, mirroring source layout (Api/, CLI/, Core/, Infrastructure/)

CodeSmith.Web/             — React frontend (Vite dev server on 5173)
  src/lib/                 — apiClient.ts (native fetch, relative /api paths)
  src/contexts/            — NavigationContext (cross-feature reset registry)
  src/features/chat/       — Tutoring surface (types, hooks, components)
  src/features/prompt-lab/ — Prompt Lab surface
  src/features/system-lab/ — System Lab surface
  src/features/home/       — Landing page
  src/features/shared/     — monacoTheme and cross-surface bits
  e2e/                     — Playwright specs
```

**Dependency direction:** `Api → Infrastructure → Core`. Core depends on nothing. Infrastructure is the only project that references SDKs, EF, or HTTP. Controllers depend only on Core interfaces; they never see an Adapter directly.

---

## Architecture Patterns

### Layering & Seams

| Seam (concern) | Interface | Implementations / Adapters |
|----------------|-----------|----------------------------|
| LLM completion (all surfaces) | `ILlmService` | `AnthropicLlmService`, `OpenAiCompatibleLlmService` (OpenAI + xAI), each wrapped by `UsageEnforcingLlmService` |
| Provider routing | `ILlmServiceFactory` | `LlmServiceFactory` (keyed DI by `AiProvider`) |
| Per-user usage lock | `IUserUsageLock` | `UserUsageLock` (singleton SemaphoreSlim registry) |
| Guidance turn (shared chat) | `IGuidanceConversation` | `GuidanceConversation` (append/trim/call/persist/rollback for all three surfaces) |
| Tutoring orchestration | `ITutoringService` | `TutoringService` |
| Problem generation | `IProblemGenerator` | `ProblemGenerator` (retry-on-truncation loop) |
| Problem parsing | `IProblemResponseParser` | `ProblemResponseParser` (DESCRIPTION/STARTER_CODE format) |
| Tutoring prompts | `ITutoringPromptTemplates` | `TutoringPromptTemplates` |
| Prompt Lab orchestration | `IPromptLabService` | `PromptLabService` |
| Prompt Lab phases (internal seams) | `IPromptSimulator`, `IPromptEvaluator`, `ITestInputGenerator` | `PromptSimulator`, `PromptEvaluator`, `TestInputGenerator` |
| System Lab orchestration | `ISystemLabService` | `SystemLabService` |
| System Lab scoring | `ISystemLabEvaluator` | `SystemLabEvaluator` |
| Session storage | `ISessionStore<T>` (+ `IPromptLabSessionStore`, `ISystemLabSessionStore`) | `InMemorySessionStore<T>` etc. (ConcurrentDictionary; `WithSessionLockAsync` serializes per-session mutation for all three surfaces) |
| Code execution | `ICodeExecutionService` | `PistonCodeExecutionService` (default) or `LocalProcessCodeExecutionService` (config-selected) |
| Piston runtime mapping | `IPistonRuntimeResolver` | `PistonRuntimeResolver` |
| Usage enforcement | `IUsageEnforcer` | `UsageEnforcer` (reserve → settle / release; free-then-paid deduction) |
| Enforcement storage | `IUsageStore` | `EfUsageStore` — one snapshot read (balance + IP aggregate) and ONE single-SaveChanges persist per enforcement phase |
| Pricing | `ILlmPricing` | `LlmPricing` (markup over `LlmPricingCatalog` — the single model↔rate source, also used by startup validation) |
| Billing-read storage | `ICreditBalanceRepository`, `IUsageLedgerRepository` | EF repositories — **billing paths only**; enforcement no longer touches them (the former `IIpFreeUsageRepository` is deleted, its behaviour absorbed by `IUsageStore`) |
| Current user identity | `ICurrentUser` | `HttpCurrentUser` (Api), `NoopCurrentUser` (Infra default) |
| Exception → HTTP | declarative mapping table inside `AppExceptionHandler` | *(not a Seam — one row per domain exception, see below)* |

### Provider routing (how an LLM call finds its Adapter)

`AiProvider` is a **runtime** value — it is stored on each session, not fixed at registration. So routing cannot use `[FromKeyedServices]`; it goes through `ILlmServiceFactory.Get(provider)`, which resolves a keyed `ILlmService`. Each provider is registered in two layers keyed by `AiProvider`: a raw adapter (singleton, under a `"raw:{provider}"` key) and the usage-enforcing decorator (scoped, under the `AiProvider` key) that wraps it. The factory is scoped, so it resolves the scoped decorator — and thus a request-scoped `IUsageEnforcer` + DbContext. Callers (e.g. `ProblemGenerator`, `PromptEvaluator`) call `_factory.Get(session.Provider).CompleteAsync(request, ct)` and get usage enforcement transparently.

> **Why two layers / why the decorator is scoped.** The decorator depends on `IUsageEnforcer` (and its DbContext), which are scoped. Registering it as a singleton would capture one DbContext for the app lifetime (a captive dependency — not thread-safe). Keeping the decorator scoped while the raw adapter stays singleton resolves this: see the [LLM Completion Seam](#llm-completion-seam-implemented-reshape) section for the full history of this reshape.

### Service lifetimes

- **Singleton:** all LLM provider Adapters and their keyed decorators (stateless), session stores (thread-safe `ConcurrentDictionary`), `ITutoringPromptTemplates`, `IProblemResponseParser`, `ILlmPricing`, `IPistonRuntimeResolver`, named `HttpClient`s.
- **Scoped:** `ILlmServiceFactory`, `IProblemGenerator`, all three `I*Service` orchestrators, all Prompt Lab / System Lab phase modules, EF repositories + `IUsageStore`, `IUsageEnforcer`, `ICurrentUser`, `CodeSmithDbContext` (**pooled** via `AddDbContextPool` when a connection string exists — pooled instances are recycled, not rebuilt, per scope), `ICodeExecutionService`.
- Rule of thumb: stateless or pure-config → singleton; anything depending on the scoped factory, the per-request user, or the DbContext → scoped.

### Middleware pipeline *(order matters)*

1. `UseExceptionHandler()` → `AppExceptionHandler` (RFC 7807 ProblemDetails via `IExceptionMapper` adapters)
2. `UseRequestLogging()` (`RequestLoggingMiddleware`)
3. Swagger (Development only)
4. `UseForwardedHeaders()` — honours `X-Forwarded-For` / `X-Forwarded-Proto` (config clears `KnownNetworks`/`KnownProxies`) so `RemoteIpAddress` is the real client IP. **Load-bearing for spend control:** both the rate limiter and the per-IP free-token cap partition on client IP.
5. `UseHttpsRedirection()`
6. `UseRateLimiter()` — fixed window, **60 requests / minute per client IP**, `QueueLimit = 0`, rejects with **429**
7. `UseCors()` — origins from `AllowedCorsOrigins` config (defaults to the HTTPS/HTTP API origins); preflight verdicts are browser-cacheable for 1h (`SetPreflightMaxAge`) so cross-origin POSTs from the SPA don't pay an OPTIONS round-trip per request
8. `UseAuthentication()` / `UseAuthorization()`
9. `MapControllers()`

### Exception → HTTP mapping

`AppExceptionHandler` owns a declarative `(Type, Status, Title, FixedDetail?)` table, matched in order with **subtype semantics** (`IsInstanceOfType` — so `TaskCanceledException` hits the `OperationCanceledException` row and still maps to 499); no match → 500 with a generic detail that never leaks the internal message. Adding an exception type means adding one table row — the lookup logic never changes. (This replaced nine one-class-per-exception `IExceptionMapper` Adapters that never varied beyond status + title.)

| Exception | Status |
|-----------|--------|
| `SessionNotFoundException` | 404 Not Found |
| `ChallengeNotFoundException` | 404 Not Found |
| `ScenarioNotFoundException` | 404 Not Found |
| `AiServiceException` | 502 Bad Gateway |
| `CodeExecutionException` | 500 Internal Server Error |
| `OperationCanceledException` | 499 Client Closed Request |
| `InsufficientQuotaException` | 402 Payment Required |
| `InvalidPriceException` | 400 Bad Request |
| `WebhookSignatureException` | 400 Bad Request |
| *(unmapped, incl. `EvaluationParseException`)* | 500 Internal Server Error |

> Note: **402** = out of quota/credits (`UsageEnforcer`); **429** = rate-limited (too many requests per IP). The full exception is logged internally; only a safe message reaches the client.

### Configuration pattern

- `appsettings.json` (defaults) + `appsettings.Development.json` (dev overrides).
- Sections: `Ai`, `Anthropic`, `OpenAi`, `Xai`, `CodeExecution`, `Usage`, `Stripe`, `AzureAd` (Entra), plus `ConnectionStrings:CodeSmithDb` and `AllowedCorsOrigins`.
- Each options class exposes a `SectionName` constant; bound via `services.Configure<T>(config.GetSection(T.SectionName))` and injected as `IOptions<T>`.
- `Ai:ActiveProvider` selects the default provider name (**default `Xai` / Grok**); `CodeExecution:Backend` selects `Piston` vs `LocalProcess` at startup.
- Each provider's `AccurateModel`/`FastModel` is **validated against the pricing catalog at startup** (`ValidateOnStart`); a model with no rate entry fails the boot rather than mis-charging silently.
- `Usage` carries `FreeMonthlyTokenQuota` (the per-objectId free cap, default 20,000 — note the name predates the move to a 48h window; it maps to `CreditBalance.FreeQuotaMax`), `PaidMarkupMultiplier` (raw-cost → charge multiplier, default `2.0`), and `AllowedDebugObjectIds` (objectIds permitted to use the dev `X-Debug-User-Id` bypass; empty in production).
- `Stripe` (`StripeOptions`) carries `SecretKey` + `WebhookSecret` (secrets — Key Vault / user-secrets), `PriceIds` (allow-list of purchasable packs), and `SuccessUrl`/`CancelUrl`. Not validated at startup (unlike provider options).

### Authentication & usage

- LLM-mutating endpoints carry `[Authorize]`. **Entra is wired:** Bearer (Entra External ID via `AddMicrosoftIdentityWebApi` bound to the `AzureAd` section) is the default scheme in all environments. In Development only, a "Debug" scheme is additionally registered and added to the default authorization policy, so allow-listed `X-Debug-User-Id` headers satisfy `[Authorize]` without a bearer token; the allow-list is `UsageOptions.AllowedDebugObjectIds`.
- `ICurrentUser.ObjectId` is the stable Entra objectId. `HttpCurrentUser` resolves it from the request (with the dev bypass); `NoopCurrentUser` is the Infrastructure default so decorator registration succeeds without the Api layer.
- Usage decorators require a non-null `ObjectId` and throw `InvalidOperationException` if absent.

---

## API Reference

All routes are under `/api`. Enums serialize as strings (`JsonStringEnumConverter`). 🔒 = `[Authorize]`.

| Method | Route | Request | Response | Notes |
|--------|-------|---------|----------|-------|
| GET | `/api/providers` | — | `{ activeProvider, availableProviders[] }` | 200 |
| POST | `/api/session` 🔒 | `CreateSessionRequest { difficulty, language, provider }` | `ProblemSession` | 201 / 400 |
| POST | `/api/session/{sessionId}/chat` 🔒 | `ChatRequest { message, editorContent?, guidanceMode? }` | `ChatResponse` | 200 / 400 / 404 |
| POST | `/api/session/{sessionId}/run` | `RunCodeRequest { language, code }` | `RunCodeResponse { stdout, stderr, exitCode, timedOut }` | 200 / 400 / 404 |
| GET | `/api/prompt-lab/challenges` | — | `ChallengeResponse[]` | hidden fields stripped |
| GET | `/api/prompt-lab/challenges/{id}` | — | `ChallengeResponse` | 200 / 404 |
| POST | `/api/prompt-lab/sessions` 🔒 | `StartChallengeRequest { challengeId, provider? }` | `PromptLabSessionResponse` | 201 / 400 / 404; generates dynamic test inputs |
| POST | `/api/prompt-lab/sessions/{sessionId}/submit` 🔒 | `SubmitAttemptRequest { systemPromptContent, userMessageContent }` | `AttemptResultResponse` | 200 / 404; simulate + evaluate |
| POST | `/api/prompt-lab/sessions/{sessionId}/chat` 🔒 | `PromptLabChatRequest { message, editorContent? }` | `PromptLabChatResponse` | 200 / 400 / 404 |
| GET | `/api/system-lab/scenarios` | — | `ScenarioResponse[]` | SecurityPitfalls stripped |
| GET | `/api/system-lab/scenarios/{id}` | — | `ScenarioResponse` | 200 / 404 |
| POST | `/api/system-lab/sessions` | `StartSystemLabSessionRequest { scenarioId, provider }` | `SystemLabSessionResponse` | 201 / 400 / 404 |
| POST | `/api/system-lab/sessions/{sessionId}/submit` 🔒 | `SubmitJustificationRequest { justificationContent }` | `SystemLabAttemptResultResponse` | 200 / 400 / 404 |
| POST | `/api/system-lab/sessions/{sessionId}/chat` 🔒 | `SystemLabChatRequest { message, currentJustification? }` | `SystemLabChatResponse` | 200 / 400 / 404 |
| POST | `/api/billing/checkout` 🔒 | `CheckoutRequest { priceId }` | `CheckoutResponse { url }` | 200 / 400 (unknown priceId); priceId must be allow-listed |
| POST | `/api/billing/webhook` | *(raw body)* + `Stripe-Signature` header | `{ result }` | **anonymous, signature-verified**; 400 bad sig / 200 processed-dup-ignored / 500 transient |
| GET | `/api/billing/balance` 🔒 | — | `BalanceResponse { paidCreditsUsd }` | 200 |
| GET | `/api/billing/ledger?take=20` 🔒 | — | `LedgerEntryResponse[] { type, amountUsd, feature?, timestampUtc }` | 200; omits `ProviderCostUsd` (margin) |

**Catalog response stripping is a security invariant:** `ChallengeResponse`/`ScenarioResponse` deliberately omit hidden fields (adversarial prompts, security pitfalls, hidden test-input expected behavior) so they never reach the client. Preserve this when editing DTO projections.

**DTO naming:** requests are `{Entity}Request`, responses are `{Entity}Response`, with static `From{Model}(...)` projection factories on the response DTOs.

---

## Key Models

| Model | Key fields |
|-------|-----------|
| `LlmResponse` | `Content`, `InputTokensUsed`, `OutputTokensUsed`, `Model`, `ContextWindowSize`, `WasTruncated` — provider-agnostic; the single return shape of every LLM call |
| `ChatMessage` | `Role` (User/Assistant), `Content`, `Timestamp` |
| `ChatResponse` | `Response`, `ContextTokensUsed`, `ContextWindowSize` |
| `ProblemSession` | `SessionId`, `Difficulty`, `Language`, `Provider`, `ProblemDescription`, `StarterCode`, `Messages[]`, `CreatedAt` |
| `PromptLabSession` | `SessionId`, `ChallengeId`, `Provider`, `TestInputs[]`, `DynamicInputsGenerated`, `Attempts[]`, `ChatHistory[]` |
| `SystemLabSession` | `SessionId`, `ScenarioId`, `Provider`, `Attempts[]`, `ChatHistory[]` |
| `Challenge` | `ChallengeId`, `Title`, `Description`, `Rubric[]`, `EditableFields[]`, `TestInputs[]`, `LockedSystemPrompt`, `HiddenAdversarialPrompt?` |
| `Scenario` | `ScenarioId`, `Title`, `Description`, `Constraints`, `EvaluationMode`, `Rubric[]`, `RequiredTradeoffs[]`, `Dimensions[]` (cross-cutting pitfalls) |
| `ChallengeAttempt` / `ScenarioAttempt` | scored result: per-criterion scores, totals, feedback (+ tradeoff results / dimension deductions for System Lab) |
| `CreditBalance` | `ObjectId`, `PaidCreditsBalance`, `FreeTokensUsedInWindow`, `FreeQuotaMax` (default 20k), `FirstSeenUtc`, `RowVersion` — free quota is a **48h window** from `FirstSeenUtc`, not a monthly reset; `RowVersion` is an optimistic-concurrency token, enforced by the billing store's retry loop (the enforcer serializes via `IUserUsageLock` instead). Seeded via static `CreditBalance.CreateNew(objectId, freeQuotaMax)`, shared by enforcement and billing |
| `UsageLedgerEntry` | `Id`, `ObjectId`, `Type` (`Spend`/`TopUp`), `Provider?`, `Model?` (null for top-ups), `InputTokens`, `OutputTokens`, `CostUsd` (Spend: amount charged = raw × markup; TopUp: amount credited), `ProviderCostUsd` (raw provider cost; null for top-ups/legacy rows), `Feature`, `TimestampUtc` — margin = `CostUsd − ProviderCostUsd` |
| `ProcessedStripeEvent` | `EventId` (PK, Stripe event id), `ProcessedUtc` — webhook dedup marker; insert-collision = already-processed |
| `IpFreeUsage` | `Ip`, `FreeTokensIssued`, `FirstSeenUtc` — per-IP aggregate of free tokens granted across all objectIds; backs the 60k-per-IP cap |
| `UsageReservation` | `ObjectId`, `ClientIp`, `Provider`, `ReservedFreeTokens`, `ReservedPaidUsd`, `UsedFree` — the upper-bound hold `ReserveAsync` returns and `SettleAsync`/`ReleaseAsync` reconcile |
| `UsageSnapshot` | `Balance?` (null = objectId never persisted), `IpFreeTokensIssued` — the full decision state one enforcement phase reads in a single `IUsageStore` call |

**Enums:** `Difficulty {Easy, Medium, Hard}`; `Language {CSharp, Cpp, Go, Rust, Python, Java, TypeScript}`; `AiProvider {Anthropic, OpenAi, Xai}`; `EvaluationMode {SingleAnswer, TradeoffReasoning, OpenJudgment}`; `LedgerEntryType {Spend=0, TopUp}`; plus `GuidanceMode`, `ChallengeCategory`, `SystemLabCategory`, `PromptFieldType`, `MessageRole`.

---

## LLM Model Selection

Each provider's options class defines two model tiers; callers pick the tier per operation.

| Operation | Tier | Anthropic default | Rationale |
|-----------|------|-------------------|-----------|
| Problem generation | Accurate | `claude-sonnet-4-6` | Once per session; quality matters |
| Tutoring / lab chat guidance | Fast | `claude-haiku-4-5-20251001` | Per-message; latency + cost |
| Prompt Lab simulation | Fast | Haiku | Parallel, one call per test input |
| Prompt Lab evaluation | Accurate | Sonnet | Rubric scoring; accuracy matters |
| Test-input generation | Accurate | Sonnet | One call at session start |
| System Lab justification evaluation | Accurate | Sonnet | Single-turn scoring |

OpenAI/xAI map the same Fast/Accurate tiers to their own model names in `OpenAiOptions` / `XaiOptions` (OpenAI defaults `gpt-4.1` / `gpt-4.1-mini`; xAI uses `grok-4.3` for **both** tiers). Context windows differ by provider — Anthropic 200,000, OpenAI ~1,047,576, xAI ~1,000,000 tokens. `ContextTokensUsed` / `ContextWindowSize` drive the frontend `TokenUsageBar` (informational only; the real spend control is the usage layer below).

> **Tier downgrade on free quota.** The usage decorator overrides the requested tier to `Fast` for *evaluation* features (`Feature` containing `"Evaluate"` or `"SystemLab"`) **while** the call is being covered by free quota inside the 48h window. Paid or post-window usage keeps `Accurate`. So an Accurate-tier evaluation can run on the Fast model when it costs the house nothing.

---

## Subsystem Architecture

### Session serialization (per-session lock)

All three surfaces hold mutable session state (chat history, attempts) in singleton in-memory stores, so concurrent requests for the **same** session must be serialized or they corrupt that shared state — e.g. two guidance turns interleaving their appends break the required user/assistant alternation and the provider rejects the next call (400). `ISessionStore<T>.WithSessionLockAsync(sessionId, action, ct)` runs `action` under a per-session lock (a `SemaphoreSlim` per id, owned by `InMemorySessionStore<T>` so callers cannot leak a held lock); different sessions run concurrently. Each orchestrator wraps its mutating operations in it: Tutoring around `GetGuidanceAsync`; Prompt Lab around `SubmitAttemptAsync` **and** `ChatAsync`; System Lab around `SubmitAttemptAsync` **and** `ChatAsync`. The lock is broader than a single turn (it also covers attempt submission), so it lives in the orchestrators, not in `IGuidanceConversation`. (Per-session locks, like the sessions themselves, are not yet evicted — a noted unbounded-growth follow-up.)

### Telemetry (OpenTelemetry → Application Insights)

Wired in `Program.cs` via the Azure Monitor distro **only when `APPLICATIONINSIGHTS_CONNECTION_STRING` is present** (set it on the Container App; local dev without it runs telemetry-off at zero cost — unlistened `StartActivity` returns null). The distro auto-instruments inbound requests, outbound HTTP (provider calls), and SqlClient (enforcement round-trips). Custom spans come from the single `CodeSmithDiagnostics.Source` (`"CodeSmith"`, Infrastructure `Diagnostics/`):

- `llm.completion` — one per Completion, tagged `codesmith.provider` / `codesmith.tier` / `codesmith.feature`; status `Error` on failure. Children: `usage.reserve`, `llm.call` (tagged `codesmith.model`, `codesmith.tokens.input/output`, `codesmith.was_truncated`), then `usage.settle` (success) or `usage.release` (failure) — so provider time vs enforcement time is separable per request.
- `problem.generation.attempt` — one per generation attempt, tagged `codesmith.attempt` / `codesmith.truncated` / `codesmith.parse_complete`, making silent retries visible.

Tests assert spans with an `ActivityListener` (`ActivityCapture` helper); span-emitting test classes share the `CodeSmithTelemetry` xUnit collection because listeners are process-global.

### Tutoring (coding problems)

`SessionController` → `ITutoringService`. Problem creation delegates to `IProblemGenerator`, which builds a prompt from `ITutoringPromptTemplates`, calls the accurate model (MaxTokens 4000 — headroom so truncation retries rarely fire; the reserve holds against it but settle refunds to actuals), and parses the `DESCRIPTION:` / `STARTER_CODE:` markers via `IProblemResponseParser`. It retries up to 2 times on truncation (`LlmResponse.WasTruncated`) or incomplete parse; each attempt emits a `problem.generation.attempt` span. Guidance is multi-turn: the service rebuilds the system prompt each turn (injecting the current editor contents) and hands the turn to the shared `IGuidanceConversation`, which owns the append/trim/call/persist/rollback mechanics; the service projects the returned completion into a `ChatResponse`. `RunCodeAsync` validates the session exists, then delegates to `ICodeExecutionService`.

### Prompt Lab (prompt engineering)

`PromptLabController` → `IPromptLabService`, which orchestrates three internal Seams. Submit is **pipelined per input**: each test input's simulate→evaluate chain is one task and all chains run in parallel, so wall clock is the slowest single chain rather than slowest-simulate + slowest-evaluate (the phase Interfaces are per-input; the orchestrator owns the fan-out):
- **`ITestInputGenerator`** — generates 4 test inputs (server pre-decides a 50/50 standard/edge split) at session start; falls back to the challenge's static inputs on failure (`DynamicInputsGenerated` records which).
- **`IPromptSimulator.SimulateOneAsync`** — runs the student's prompt against one test input (fast model), combining locked + adversarial + user prompt content into a `SimulatedInput`. Effective system prompt = `[LockedSystemPrompt] + [HiddenAdversarialPrompt] + [UserSystemPromptEdits]`; the adversarial segment is invisible to the user and cannot be overridden (anti-gaming).
- **`IPromptEvaluator.EvaluateOneAsync`** — scores one output against the rubric in isolation (accurate model), returning JSON parsed into `CriterionScore`s; `AssembleAttempt` is the pure aggregation of the per-input results into the scored `ChallengeAttempt` (totals + overall feedback).

All three phases parse model output through the shared **`LlmJson`** Module (fence-stripping, one failure mode: `EvaluationParseException`, rubric-integrity walk — see [Ubiquitous Language](#ubiquitous-language)); the `{input}` placeholder substitution both simulate and evaluate apply to the student's template lives in **`TestInputMessage`**, so the output scored is always the output generated.

Chat is Socratic guidance delegated to the shared `IGuidanceConversation` (20-message sliding window, user turn rolled back on failure). `ChallengeCatalog` is a static in-memory collection (categories × difficulties, each with a locked prompt, hidden adversarial prompt, test inputs, and rubric).

### System Lab (system design)

`SystemLabController` → `ISystemLabService` → `ISystemLabEvaluator`. The evaluator builds a mode-specific system prompt (`SingleAnswer` / `TradeoffReasoning` / `OpenJudgment`), generates the JSON schema dynamically from the scenario's cross-cutting dimensions, calls the accurate model, and parses criterion scores (via the shared `LlmJson` Module), tradeoff engagement, and dimension deductions — clamping every value and **dropping hallucinated criterion IDs and dimension names** so the evaluator can neither invent points nor invent penalties. Chat delegates to the shared `IGuidanceConversation`. System Lab holds the per-session lock around **both** submit and chat (it is broader than a single guidance turn), so it stays in the orchestrator rather than `IGuidanceConversation` — see [Session serialization](#session-serialization-per-session-lock).

### Usage & credits (cost protection)

Every keyed LLM registration wraps the provider Adapter in a usage-enforcing decorator that runs **reserve → call → settle** (or **release** on failure) around each call. Two free axes plus a paid balance gate every Completion:

- **Per-objectId free window** — `CreditBalance.FreeQuotaMax` tokens (default 20,000) available only during the **48 hours** after `FirstSeenUtc` (first sighting of the objectId). There is **no monthly reset**; once the window closes, free quota is zero.
- **Per-IP free aggregate** — a **60,000-token** total cap on free tokens issued from one client IP across *all* objectIds (`IpFreeUsage`, read/written through `IUsageStore`), so many fresh objectIds behind one IP cannot farm unlimited free usage.
- **Paid credits** — `PaidCreditsBalance`, debited in USD-equivalent when free coverage is exhausted.

**Pricing (`ILlmPricing` + `LlmPricingCatalog`).** `LlmPricingCatalog` is the **single source of truth** binding `(provider, model)` to **raw provider cost** per 1k tokens. A config markup (`UsageOptions.PaidMarkupMultiplier`, default `2.0`) turns raw cost into the **customer charge**; `ComputeCostUsd` vs `ComputeChargeUsd` are kept separate so the ledger records both — `UsageLedgerEntry.CostUsd` (charge) and `ProviderCostUsd` (raw) — keeping margin reportable. **Drift is prevented at startup:** each provider's configured `AccurateModel`/`FastModel` is validated against the catalog via `Options.Validate(...).ValidateOnStart()` — an unpriced model **fails the app boot** with a message naming the provider + tier (see `AddValidatedProviderOptions` in the composition root). The runtime "unknown model → global highest rate" fallback in `ComputeCostUsd` is therefore unreachable for configured models (adapters stamp `LlmResponse.Model` with the configured name); if it ever fires it **logs a warning** and over-charges-safe rather than failing the live request.

The seam is `IUsageEnforcer`, a reserve → settle / release lifecycle (`UsageReservation` is the hold that crosses it):

1. **Reserve** (`ReserveAsync`): estimates input tokens (≈ chars/4 + overhead) and treats `MaxTokens` as the output estimate, computes an **upper-bound** charge (global highest rate × markup), and admits the call if the free window quota (bounded by both the objectId remainder and the IP remainder) **or** paid credits cover it — including a **partial-free** path where free covers part and paid covers the overflow; otherwise throws `InsufficientQuotaException` (→ 402). Crucially it then **persists the hold** (free tokens against the window + IP aggregate, paid charge against `PaidCreditsBalance`) before releasing the lock, and returns a `UsageReservation` describing exactly what was held.
2. **Settle** (`SettleAsync`): reverses the hold, then from the response's *actual* tokens deducts **free first** (within the active window, capped by both the objectId and IP remainders) then **paid credits** for the remainder (charge prorated by the paid token fraction); appends a `UsageLedgerEntry` tagged with a `Feature` string (e.g. `"PromptLab:Evaluate"`). The upper-bound hold ≥ actual, so the reconcile only ever refunds.
3. **Release** (`ReleaseAsync`): reverses the hold with no ledger entry — used by the decorator when the LLM call throws, so a failed call consumes no quota.

All three run under a **per-user lock** (`IUserUsageLock`); IP-aggregate adjustments take an additional `ip:{ip}` lock, so neither the shared scoped DbContext nor the 60k cap loses an update.

**Storage crosses the deep `IUsageStore` seam** (`UsageSnapshot` = balance + IP-issued total in one read; `PersistAsync` lands the phase outcome — balance write, IP delta, optional ledger row — in ONE `SaveChangesAsync`). Each phase therefore costs ~3 serialized DB round-trips instead of the 4–7 the old three-repository composition produced (~6 per Completion, down from ~9–13); the single-save invariant is pinned by `EfUsageStoreTests` with a SaveChanges-counting interceptor. A missing balance row is materialized via `CreditBalance.CreateNew` in the enforcer (Reserve/Settle) but **never in Release** — reversing a paid hold onto a fresh row would mint credits.

> **Reservation honesty — closed in-process (was the headline cost gap).** Because the hold is now *persisted at reserve time*, concurrent completions for one user (a Prompt Lab submit fans out to up to 2N parallel Completions — N simulate + N evaluate, both `Task.WhenAll`) serialize on the per-user lock and each sees the prior holds; they can no longer all pass a gate that only had budget for one. **Still open:** the in-process `UserUsageLock` makes this correct on a single instance only. For a multi-instance deployment, `CreditBalance.RowVersion` would be the cross-process guard for the enforcer — the enforcer does not yet use it (relying on the in-process lock), though the billing store already does (see below). Deferred for enforcement.

### Billing (Stripe prepaid credits)

A module **separate from usage enforcement**: billing *writes* credits, enforcement *debits* them. Billing code never references `IUsageEnforcer`, `IUserUsageLock`, or any LLM service; `objectId` comes only from `ICurrentUser`. `BillingController` → `IBillingService` (Core, carries no Stripe types) → `StripeBillingService` (Infrastructure `Billing/`).

- **Checkout** (`POST /api/billing/checkout`, 🔒): validates `priceId` against `StripeOptions.PriceIds` (else `InvalidPriceException` → 400), creates a hosted Stripe Checkout session (`mode=payment`, `metadata["objectId"] = currentUser.ObjectId`), returns `session.Url`.
- **Webhook** (`POST /api/billing/webhook`, **anonymous**): reads the **raw body** (no model binding — signature hashes exact bytes) and the `Stripe-Signature` header. Verification is isolated behind the internal `IStripeEventReader` seam (over `EventUtility.ConstructEvent`) so the handler is unit-testable without minting signatures; a mismatch → `WebhookSignatureException` → 400. On `checkout.session.completed` it guards currency == `usd`, presence of `objectId` metadata, and positive `amount_total`, then credits `amount_total / 100` USD. HTTP contract: **400** bad sig · **200** processed/duplicate/ignored · **500** transient (Stripe retries).
- **Idempotency + atomicity** — the credit lives in the deep `IStripeCreditStore` / `EfStripeCreditStore`, which in **one `SaveChangesAsync`** inserts the `ProcessedStripeEvent` dedup marker, credits `PaidCreditsBalance` (creating the row via `GetOrCreateAsync` for a payer who buys before their first LLM call), and appends a `TopUp` `UsageLedgerEntry`. A re-seen event id (or PK-collision race) returns `AlreadyProcessed` with no change; a concurrent enforcement write surfaces as `DbUpdateConcurrencyException` and is retried against `CreditBalance.RowVersion`. Stripe delivers at-least-once, so this replay-safety is load-bearing.
- **Reads** (🔒): `GET /balance` returns paid credits; `GET /ledger?take=N` returns recent rows as DTOs that **omit `ProviderCostUsd`** (raw cost / margin) and `RowVersion`.

### Code execution

Config key `CodeExecution:Backend` selects the Adapter at startup: **`Piston`** (default) posts to a Dockerized sandbox via a named, resilience-wrapped `HttpClient`, mapping `Language` → Piston runtime through `IPistonRuntimeResolver`; **`LocalProcess`** runs code directly on the host (dev fallback only). Any other value throws at composition time.

---

## LLM Completion Seam (Implemented Reshape)

> **Status: implemented** (branch `refactor/llm-completion-seam`). This section records the shape of the LLM Seam and the vocabulary it introduces. It replaced the former three capability interfaces (`ITutoringLlmService` / `IPromptLabLlmService` / `ISystemLabLlmService`), which expanded into seven named methods, three usage decorators, a copy-paste `XaiLlmService`, and nine keyed registrations.

### Why

The current Seam is shaped by **caller intent** (`GenerateProblemAsync`, `SimulatePromptAsync`, `EvaluateResponseAsync`, `GenerateTestInputsAsync`, `EvaluateJustificationAsync`, `GetGuidanceAsync`) rather than by the **operation**. Every method collapses to one of two real shapes — a completion over a message list at a chosen model tier — differing only by (a) which model tier and (b) a log/feature label. The capability shaping multiplies complexity by provider count (9 keyed registrations, 3 duplicate decorators, a copy-paste `XaiLlmService`). It is a **shallow** Seam: the Interface is as complex as the behaviour behind it.

### Target shape (locked decisions)

One Interface, one method:

```csharp
public interface ILlmService
{
    Task<LlmResponse> CompleteAsync(CompletionRequest request, CancellationToken ct = default);
}
```

`CompletionRequest` is a parameter object carrying caller intent as **data**:

```csharp
public sealed record CompletionRequest
{
    public required string SystemPrompt { get; init; }
    public required IReadOnlyList<ChatMessage> Messages { get; init; }  // single-turn = one User message
    public required ModelTier Tier { get; init; }                       // no default — choosing tier is a cost decision
    public required int MaxTokens { get; init; }
    public required string Feature { get; init; }                       // flows to error logs + UsageLedgerEntry.Feature

    // Trivial common case for the 5 single-turn callers:
    public static CompletionRequest SingleTurn(string system, string user, ModelTier tier, int maxTokens, string feature) => ...;
}

public enum ModelTier { Fast, Accurate }   // two tiers today; extend here if a third is ever needed
```

**Locked design decisions (do not re-litigate without an ADR):**
1. **One method, not two.** Single-turn is a one-element `Messages` list; `CompletionRequest.SingleTurn(...)` keeps the common case a one-liner while the Adapter implements a single code path.
2. **`ModelTier` enum, two values.** The caller picks the tier (intent); each Adapter maps `Tier → model string` from its own options (Locality). Fast/Accurate are the only foreseeable tiers; the enum is the extension point if that changes.
3. **`Feature` is a `string`.** Friction-free growth across the three surfaces; matches `UsageLedgerEntry.Feature`. Type safety was explicitly declined.
4. **Keep the factory, drop the generic.** `ILlmServiceFactory.Get(AiProvider) → ILlmService`. Provider is a runtime value, so a resolver over keyed DI is still required; the factory stays a deep little Module hiding keyed resolution.

### What falls out

- The three capability interfaces disappear; each provider implements `ILlmService` once.
- `OpenAiLlmService` and `XaiLlmService` collapse to one **OpenAI-compatible** Adapter whose only variation is endpoint + credential at the Seam (xAI becomes configuration, not a class).
- The three usage decorators collapse to **one** decorator over `ILlmService` that reads `request.Feature`.
- Keyed registrations drop from 9 to one-per-provider.
- Test surface drops from 7 methods × 3 providers to `CompleteAsync` (a single-turn + a multi-turn assertion) × 2 Adapters; every caller test mocks the same `ILlmService.CompleteAsync → LlmResponse` idiom.

### Adapter transport configuration

Both Adapters run with an **explicit 120s per-call timeout** (`TimeoutSeconds` on each provider's options — the Anthropic SDK otherwise defaults to 10 minutes) and **zero transport auto-retry** (`Anthropic:MaxRetries` default 0; the OpenAI pipeline gets `ClientRetryPolicy(maxRetries: 0)`). Auto-retry is deliberately off: a transport-level retry re-runs a *metered* Completion — invisible provider cost and multiplied worst-case latency. Retryable failures surface as `AiServiceException` (502) and the reservation is released, so the user retries explicitly. Both are pinned by adapter tests (`CallCount == 1` on a 503; timeout via the internal client view).

### Adapter test seam & cancellation semantics

Each Adapter accepts an optional `HttpClient` (defaulting to the SDK's own transport) — an **internal seam** used only by the Adapter's tests, which pin the full translation contract at the HTTP level with a stub handler: outgoing request (tier→configured model, max tokens, system-prompt placement, role mapping, endpoint routing incl. xAI's base URL) and response mapping (**configured**-model stamping — never the served name, which is what keeps the pricing-catalog fallback unreachable — token counts, `WasTruncated`, multi-block text concatenation). `LlmServiceFactoryTests` pins the two-layer keyed registration through the real composition root (raw Adapters singleton, decorators scoped). Cancellation: caller-initiated cancellation (`ct` requested) propagates as `OperationCanceledException` → 499; a provider-side HTTP timeout is also an OCE but with an un-cancelled token, so it still wraps to `AiServiceException` → 502.

### What we give up

Compile-time capability segregation (e.g. a System Lab caller could request any completion). This protection was illusory — the method bodies were identical — and the real invariants (tier, feature) become explicit data on the request.

---

## Frontend Conventions

- **Feature-based folders** under `src/features/{chat,prompt-lab,system-lab,home}`, each with `components/`, `hooks/`, and `types.ts`. Shared bits in `src/features/shared/`.
- **API access** goes through `src/lib/apiClient.ts` only — native `fetch`, relative `/api` paths (Vite proxies to `http://localhost:5175`), a single `request<T>` helper that throws `ApiClientError` (carrying status + `ApiError` body) on non-2xx. API functions are plain exported functions, not class methods.
- **All server calls are TanStack Query** mutations/queries in `hooks/` (`useCreateSession`, `useSendMessage`, `useStartChallenge`, `useSubmitAttempt`, `use*Chat`, etc.). No raw `useEffect` + `fetch`. `QueryClient` is configured in `App.tsx` (`retry: 1`, `refetchOnWindowFocus: false`).
- The three surfaces currently duplicate a parallel hook/API/type structure (start / submit / chat per lab) — a candidate consolidation, lower priority than the backend LLM Seam.
- State: component `useState` for UI ephemera; session data held in component state after mutation success; `NavigationContext` provides cross-component reset callbacks. No Redux.
- TypeScript `strict: true`, no `any`, named exports. Enum-like values are string-literal unions with `is{Type}()` guards.
- Tailwind v4 via `@import "tailwindcss"` (no config file); palette aligned to **VS Code Dark Modern**. Editors use Monaco (`monacoTheme.ts`); `test/setup.ts` mocks Monaco.

### Component hierarchy (Chat surface)
```
ChatWindow
├── DifficultySelector  — initial setup before session starts
├── ChatPanel           — problem description + message list (MessageBubble per message)
├── CodePanel           — Monaco editor + TerminalPanel
└── (divider)           — useResizableSplit (percentage-based)
```

---

## Naming & Code Style

### Backend
| Thing | Convention | Example |
|-------|-----------|---------|
| Interface | `I` + role | `ILlmServiceFactory` |
| LLM provider Adapter | `{Provider}LlmService` | `AnthropicLlmService` |
| Orchestrator | `{Surface}Service` | `PromptLabService` |
| Options | `{Area}Options` + `SectionName` const | `AnthropicOptions` |
| Request / Response DTO | `{Entity}Request` / `{Entity}Response` | `CreateSessionRequest` |

- **Block titles:** `// == Title Here == //` at the head of every function/class/important block (adapt the comment syntax per language).
- **XML docs:** `/// <summary>` only at class/interface/file level — never on individual members, properties, or enum values. Use inline `//` on the same line for member-level notes; align inline comments where practical. No `/// <inheritdoc />`.
- C#: 4-space indent, PascalCase types/methods, camelCase locals.
- Prefer editing/refactoring existing code over adding new files. When overwriting a file, explain what changed, why, and the downstream effect.

### Frontend
| Thing | Convention | Example |
|-------|-----------|---------|
| Component | PascalCase `.tsx` | `ChatWindow.tsx` |
| Hook | `use` + camelCase | `useSubmitAttempt.ts` |
| Props interface | `{Component}Props` | `ChatWindowProps` |
| Test | colocated `*.test.tsx` | `MessageBubble.test.tsx` |

- TS: 2-space indent, `strict: true`, no `any`, named exports only.

---

## Testing Conventions

- **TDD is the default** — write the test first when adding behaviour. The **Interface is the test surface**: assert on observable outcomes through a Module's Interface, not its internals. When a Module is deepened, delete the old shallow-module tests rather than layering new ones on top.
- **Backend (xUnit + NSubstitute):** tests live in `CodeSmith.Tests/{Layer}/{SourceClass}Tests.cs`. Method naming `{Method}_{Condition}_{Expected}`. Mock with `Substitute.For<IInterface>()` + `.Returns()`/`.Throws()`; assert with `Assert.Equal`, `Assert.IsType<T>`, `Assert.Received()`. LLM-dependent modules substitute `ILlmServiceFactory` / the LLM Interface and return canned `LlmResponse`. (After the LLM Seam reshape, this becomes a single `ILlmService.CompleteAsync` mock idiom everywhere.)
- **Frontend unit (Vitest + RTL):** colocated `*.test.tsx`; `describe()` per component; `userEvent.setup()` for interactions; `vi.mock()`/`vi.fn()` for modules/callbacks. No snapshot tests.
- **E2E (Playwright):** `CodeSmith.Web/e2e/`. Browser/Playwright-MCP checks are run **only when explicitly requested**, never automatically after frontend changes.

---

## Dev Commands

```bash
# Backend API (HTTPS 7111, HTTP 5175)
cd CodeSmith.Api && dotnet run

# Frontend (Vite on 5173)
cd CodeSmith.Web && npm run dev

# Backend tests
cd CodeSmith.Tests && dotnet test

# Frontend unit tests
cd CodeSmith.Web && npm test

# E2E
cd CodeSmith.Web && npx playwright test
```

---

## Ubiquitous Language

Shared vocabulary. Each word has an ordinary meaning too — these are the project-specific senses.

**Module** — anything with an Interface and an Implementation; scale-agnostic (function, class, package, tier-spanning slice).
**Interface** — everything a caller must know to use a Module correctly: type signature *plus* invariants, ordering, error modes, required config, performance characteristics.
**Implementation** — the code inside a Module.
**Depth** — leverage at the Interface: a large amount of behaviour behind a small Interface. **Deep** = high leverage; **shallow** = Interface nearly as complex as the Implementation.
**Seam** — a place where behaviour can be altered without editing in place; the location where a Module's Interface lives.
**Adapter** — a concrete thing satisfying an Interface at a Seam (describes role, not substance).
**Leverage** — what callers get from Depth: more capability per unit of Interface learned.
**Locality** — what maintainers get from Depth: change, bugs, knowledge, verification concentrated in one place.

**Deletion test** — imagine deleting a Module. If complexity vanishes, it was a pass-through; if complexity reappears across N callers, it earned its keep.
**One adapter = hypothetical Seam; two adapters = real Seam.** Don't introduce a port unless behaviour actually varies across it (typically production + test).

### Domain terms introduced by the LLM Completion Seam reshape
**Completion** — a single LLM call: a system prompt + a message list answered at a model tier. The one operation behind the reshaped LLM Seam.
**CompletionRequest** — the parameter object carrying a Completion's inputs and caller intent (system prompt, messages, tier, max tokens, feature).
**ModelTier** — the caller-chosen quality/cost tier (`Fast`, `Accurate`); each provider Adapter maps a tier to a concrete model name.
**Feature** — a free-form `string` label identifying what a Completion is for (e.g. `"Tutoring:Guidance"`); flows to error logs and the usage ledger.

### Domain terms introduced by the LLM JSON parsing Module
**LlmJson** — the shared Module owning defensive parsing of Completion content that is expected to be JSON: markdown-fence stripping (`ExtractJson`), document parsing and typed deserialization with a **single failure mode** (`EvaluationParseException`), and the one rubric-integrity walk (`ParseCriterionScores`: entries with missing or hallucinated criterion IDs are dropped, fractional points rounded, missing points default to 0, scores clamped to `[0, MaxPoints]`). A `static` class in Infrastructure, deliberately **not** a Seam — nothing varies behind it, and keeping it un-mockable forces evaluator tests through the real parse path. Consumers: `PromptEvaluator`, `SystemLabEvaluator`, `TestInputGenerator`.
**TestInputMessage** — the Prompt Lab Module that builds the effective user message for a test input from the student's template: `{input}` placeholder substitution (case-insensitive), or appending the input when no placeholder exists. Shared by the simulate and evaluate phases so both operate on the same message.

### Domain terms introduced by the Guidance Conversation Seam
**Guidance Conversation** — the multi-turn Socratic exchange a student has with a surface's tutor (Tutoring, Prompt Lab, or System Lab). The deep Module that owns one round of it is `IGuidanceConversation`; each surface keeps its own system-prompt builder and supplies the result as data.
**Guidance Turn** — a single round of a Guidance Conversation, the unit behind `IGuidanceConversation.RunTurnAsync(provider, history, GuidanceTurnRequest, persist, ct)`. It owns the full mutation/error invariant in one place: append the user message → trim history to a whole-turn window anchored on a User message → run one Fast-tier Completion → append the assistant reply → persist; on non-domain failure, roll the user turn back and surface `AiServiceException`. Replaces the three hand-copied chat flows in `TutoringService.GetGuidanceAsync`, `PromptLabService.ChatAsync`, and `SystemLabService.ChatAsync` (which diverged on rollback, error-wrapping, and trimming).
