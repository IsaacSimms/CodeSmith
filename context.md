# CodeSmith — Context & Architecture Reference

CodeSmith is an AI-powered practice tool for technical interviews. It hosts three independent practice surfaces — **Tutoring** (coding problems with a Socratic pair-programmer), **Prompt Lab** (prompt-engineering challenges scored against a rubric), and **System Lab** (system-design justification scenarios) — over a shared, provider-agnostic LLM layer. Every LLM call is metered against a per-user free quota and paid credit balance so the SaaS cannot be run at a loss.

This document is the ground-truth architectural reference. It reflects the repo as of 2026-06-19 (reviewed 2026-06-23). Keep the Seams table, API Reference, subsystem sections, and the [Ubiquitous Language](#ubiquitous-language) glossary updated as the architecture evolves.

> **Vocabulary note.** This project uses a deliberate architecture vocabulary — **Module, Interface, Implementation, Depth, Seam, Adapter, Leverage, Locality**. Definitions are in the [Ubiquitous Language](#ubiquitous-language) section at the end. Use these terms exactly; do not substitute "component / service / boundary."

---

## Stack

| Layer          | Technology                                      |
|----------------|-------------------------------------------------|
| Backend        | .NET 8, ASP.NET Core Web API                    |
| LLM providers  | Anthropic SDK; OpenAI SDK (also drives xAI/Grok via OpenAI-compatible endpoint) |
| Persistence    | EF Core + SQL Server (usage/credits); in-memory session stores |
| Code execution | Piston (Docker sandbox, default) or LocalProcess (dev fallback) |
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
  Enums/                   — AiProvider, Difficulty, Language, MessageRole, GuidanceMode,
                             EvaluationMode, ChallengeCategory, SystemLabCategory, PromptFieldType
  Exceptions/              — Domain exceptions (each maps to one HTTP status, see below)
  Interfaces/              — All seams live here (ILlm*, I*Service, ISessionStore, IUsage*, etc.)
  Models/                  — ChatMessage, LlmResponse, ProblemSession, CodeExecutionResult,
                             PromptLab/*, SystemLab/*, Usage/*

CodeSmith.Infrastructure/  — Implementations of Core interfaces; the only project that touches SDKs/EF/HTTP
  Configuration/           — Options classes (Anthropic, OpenAi, Xai, Ai, CodeExecution, Usage)
  DependencyInjection/     — ServiceCollectionExtensions.AddCodeSmithInfrastructure (composition root)
  Persistence/             — CodeSmithDbContext + EF repositories (credit balance, usage ledger)
  Services/                — LLM adapters, generators, lab orchestrators, session stores
    PromptLab/             — ChallengeCatalog, PromptSimulator, PromptEvaluator, TestInputGenerator, PromptLabService
    SystemLab/             — ScenarioCatalog, SystemLabEvaluator, SystemLabService
    Piston/                — Sandboxed code-execution adapter + runtime resolver
    Usage/                 — UsageEnforcer, LlmPricing, NoopCurrentUser, Decorators/

CodeSmith.Api/             — ASP.NET Core host (HTTPS 7111, HTTP 5175)
  Controllers/             — SessionController, PromptLabController, SystemLabController
  DTOs/                    — Request/response shapes per surface (PromptLab/, SystemLab/)
  Middleware/              — AppExceptionHandler + IExceptionMapper adapters; RequestLoggingMiddleware
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
| Tutoring orchestration | `ITutoringService` | `TutoringService` |
| Problem generation | `IProblemGenerator` | `ProblemGenerator` (retry-on-truncation loop) |
| Problem parsing | `IProblemResponseParser` | `ProblemResponseParser` (DESCRIPTION/STARTER_CODE format) |
| Tutoring prompts | `ITutoringPromptTemplates` | `TutoringPromptTemplates` |
| Prompt Lab orchestration | `IPromptLabService` | `PromptLabService` |
| Prompt Lab phases (internal seams) | `IPromptSimulator`, `IPromptEvaluator`, `ITestInputGenerator` | `PromptSimulator`, `PromptEvaluator`, `TestInputGenerator` |
| System Lab orchestration | `ISystemLabService` | `SystemLabService` |
| System Lab scoring | `ISystemLabEvaluator` | `SystemLabEvaluator` |
| Session storage | `ISessionStore<T>` (+ `IPromptLabSessionStore`, `ISystemLabSessionStore`) | `InMemorySessionStore<T>` etc. (ConcurrentDictionary) |
| Code execution | `ICodeExecutionService` | `PistonCodeExecutionService` (default) or `LocalProcessCodeExecutionService` (config-selected) |
| Piston runtime mapping | `IPistonRuntimeResolver` | `PistonRuntimeResolver` |
| Usage enforcement | `IUsageEnforcer` | `UsageEnforcer` (free-then-paid deduction) |
| Pricing | `ILlmPricing` | `LlmPricing` (versioned rate table) |
| Credit/ledger storage | `ICreditBalanceRepository`, `IUsageLedgerRepository` | EF repositories |
| Current user identity | `ICurrentUser` | `HttpCurrentUser` (Api), `NoopCurrentUser` (Infra default) |
| Exception → HTTP | `IExceptionMapper` | one Adapter per domain exception (see below) |

### Provider routing (how an LLM call finds its Adapter)

`AiProvider` is a **runtime** value — it is stored on each session, not fixed at registration. So routing cannot use `[FromKeyedServices]`; it goes through `ILlmServiceFactory.Get(provider)`, which resolves a keyed `ILlmService`. Each provider is registered in two layers keyed by `AiProvider`: a raw adapter (singleton, under a `"raw:{provider}"` key) and the usage-enforcing decorator (scoped, under the `AiProvider` key) that wraps it. The factory is scoped, so it resolves the scoped decorator — and thus a request-scoped `IUsageEnforcer` + DbContext. Callers (e.g. `ProblemGenerator`, `PromptEvaluator`) call `_factory.Get(session.Provider).CompleteAsync(request, ct)` and get usage enforcement transparently.

> **Why two layers / why the decorator is scoped.** The decorator depends on `IUsageEnforcer` (and its DbContext), which are scoped. Registering it as a singleton would capture one DbContext for the app lifetime (a captive dependency — not thread-safe). Keeping the decorator scoped while the raw adapter stays singleton resolves this: see the [LLM Completion Seam](#llm-completion-seam-implemented-reshape) section for the full history of this reshape.

### Service lifetimes

- **Singleton:** all LLM provider Adapters and their keyed decorators (stateless), session stores (thread-safe `ConcurrentDictionary`), `ITutoringPromptTemplates`, `IProblemResponseParser`, `ILlmPricing`, `IPistonRuntimeResolver`, named `HttpClient`s.
- **Scoped:** `ILlmServiceFactory`, `IProblemGenerator`, all three `I*Service` orchestrators, all Prompt Lab / System Lab phase modules, EF repositories, `IUsageEnforcer`, `ICurrentUser`, `CodeSmithDbContext`, `ICodeExecutionService`.
- Rule of thumb: stateless or pure-config → singleton; anything depending on the scoped factory, the per-request user, or the DbContext → scoped.

### Middleware pipeline *(order matters)*

1. `UseExceptionHandler()` → `AppExceptionHandler` (RFC 7807 ProblemDetails via `IExceptionMapper` adapters)
2. `UseRequestLogging()` (`RequestLoggingMiddleware`)
3. Swagger (Development only)
4. `UseHttpsRedirection()`
5. `UseRateLimiter()` — fixed window, **60 requests / minute per client IP**, `QueueLimit = 0`, rejects with **429**
6. `UseCors()` — origins from `AllowedCorsOrigins` config (defaults to the HTTPS/HTTP API origins)
7. `UseAuthentication()` / `UseAuthorization()`
8. `MapControllers()`

### Exception → HTTP mapping

`AppExceptionHandler` iterates registered `IExceptionMapper` Adapters and returns the first non-null `ProblemDetails`; no mapper → 500. Adding an exception type means adding one mapper registration — the handler never changes (deep Seam).

| Exception | Status |
|-----------|--------|
| `SessionNotFoundException` | 404 Not Found |
| `ChallengeNotFoundException` | 404 Not Found |
| `ScenarioNotFoundException` | 404 Not Found |
| `AiServiceException` | 502 Bad Gateway |
| `CodeExecutionException` | 500 Internal Server Error |
| `OperationCanceledException` | 499 Client Closed Request |
| `InsufficientQuotaException` | 402 Payment Required |
| *(unmapped, incl. `EvaluationParseException`)* | 500 Internal Server Error |

> Note: **402** = out of quota/credits (`UsageEnforcer`); **429** = rate-limited (too many requests per IP). The full exception is logged internally; only a safe message reaches the client.

### Configuration pattern

- `appsettings.json` (defaults) + `appsettings.Development.json` (dev overrides).
- Sections: `Ai`, `Anthropic`, `OpenAi`, `Xai`, `CodeExecution`, `Usage`, plus `ConnectionStrings:CodeSmithDb` and `AllowedCorsOrigins`.
- Each options class exposes a `SectionName` constant; bound via `services.Configure<T>(config.GetSection(T.SectionName))` and injected as `IOptions<T>`.
- `Ai:ActiveProvider` selects the default provider name; `CodeExecution:Backend` selects `Piston` vs `LocalProcess` at startup.

### Authentication & usage

- LLM-mutating endpoints carry `[Authorize]`. In Development a "Debug" scheme (registered only under `IsDevelopment()`) accepts allow-listed `X-Debug-User-Id` headers to satisfy auth. Full Entra (`AddMicrosoftIdentityWebApi`) wiring is planned for later.
- `ICurrentUser.ObjectId` is the stable Entra objectId. `HttpCurrentUser` resolves it from the request (with a dev bypass); `NoopCurrentUser` is the Infrastructure default so decorator registration succeeds without the Api layer.
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
| `SystemLabSession` | `SessionId`, `ScenarioId`, `Provider`, `Attempts[]`, `ChatHistory[]` (mutations guarded by a per-session `SemaphoreSlim`) |
| `Challenge` | `ChallengeId`, `Title`, `Description`, `Rubric[]`, `EditableFields[]`, `TestInputs[]`, `LockedSystemPrompt`, `HiddenAdversarialPrompt?` |
| `Scenario` | `ScenarioId`, `Title`, `Description`, `Constraints`, `EvaluationMode`, `Rubric[]`, `RequiredTradeoffs[]`, `Dimensions[]` (cross-cutting pitfalls) |
| `ChallengeAttempt` / `ScenarioAttempt` | scored result: per-criterion scores, totals, feedback (+ tradeoff results / dimension deductions for System Lab) |
| `CreditBalance` | `ObjectId`, `FreeQuotaMax`, `FreeTokensUsedThisMonth`, `LastFreeResetUtc`, `PaidCreditsBalance` |
| `UsageLedgerEntry` | `ObjectId`, `Provider`, `Model`, `InputTokens`, `OutputTokens`, `CostUsd`, `Feature`, `TimestampUtc` |

**Enums:** `Difficulty {Easy, Medium, Hard}`; `Language {CSharp, Cpp, Go, Rust, Python, Java, TypeScript}`; `AiProvider {Anthropic, OpenAi, Xai}`; `EvaluationMode {SingleAnswer, TradeoffReasoning, OpenJudgment}`; plus `GuidanceMode`, `ChallengeCategory`, `SystemLabCategory`, `PromptFieldType`, `MessageRole`.

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

OpenAI/xAI map the same Fast/Accurate tiers to their own model names in `OpenAiOptions` / `XaiOptions`. Context window (Anthropic) is 200,000 tokens. `ContextTokensUsed` / `ContextWindowSize` drive the frontend `TokenUsageBar` (informational only; the real spend control is the usage layer below).

---

## Subsystem Architecture

### Tutoring (coding problems)

`SessionController` → `ITutoringService`. Problem creation delegates to `IProblemGenerator`, which builds a prompt from `ITutoringPromptTemplates`, calls the accurate model, and parses the `DESCRIPTION:` / `STARTER_CODE:` markers via `IProblemResponseParser`. It retries up to 2 times on truncation (`LlmResponse.WasTruncated`) or incomplete parse. Guidance is multi-turn: the user turn is appended to `session.Messages`, the system prompt is rebuilt each turn (it injects the current editor contents), and the fast model answers. `RunCodeAsync` validates the session exists, then delegates to `ICodeExecutionService`.

### Prompt Lab (prompt engineering)

`PromptLabController` → `IPromptLabService`, which orchestrates three internal Seams:
- **`ITestInputGenerator`** — generates 4 test inputs (server pre-decides a 50/50 standard/edge split) at session start; falls back to the challenge's static inputs on failure (`DynamicInputsGenerated` records which).
- **`IPromptSimulator`** — runs the student's prompt against every test input **in parallel** (fast model), combining locked + adversarial + user prompt content. Effective system prompt = `[LockedSystemPrompt] + [HiddenAdversarialPrompt] + [UserSystemPromptEdits]`; the adversarial segment is invisible to the user and cannot be overridden (anti-gaming).
- **`IPromptEvaluator`** — scores each output against the rubric **in parallel** (accurate model), returning JSON parsed into `CriterionScore`s.

Chat is Socratic guidance with a 20-turn sliding history window; the user turn is rolled back if the LLM call fails. `ChallengeCatalog` is a static in-memory collection (categories × difficulties, each with a locked prompt, hidden adversarial prompt, test inputs, and rubric).

### System Lab (system design)

`SystemLabController` → `ISystemLabService` → `ISystemLabEvaluator`. The evaluator builds a mode-specific system prompt (`SingleAnswer` / `TradeoffReasoning` / `OpenJudgment`), generates the JSON schema dynamically from the scenario's cross-cutting dimensions, calls the accurate model, and parses criterion scores, tradeoff engagement, and dimension deductions — clamping every value and **dropping hallucinated criterion IDs** to prevent phantom points. Unlike the other surfaces, System Lab session mutation is guarded by a **per-session `SemaphoreSlim`** (`ISystemLabSessionStore.GetLock`).

### Usage & credits (cost protection)

Every keyed LLM registration wraps the provider Adapter in a usage-enforcing decorator that runs **check → call → record** around each call:
1. **Check** (`IUsageEnforcer.CheckAndReserveAsync`): estimates input tokens (≈ chars/4 + overhead), computes an **upper-bound** cost using the global highest rate, and throws `InsufficientQuotaException` (→ 402) if neither free quota nor paid credits cover it.
2. **Record** (`RecordActualAsync`): computes actual cost via `ILlmPricing` (versioned per-model rate table), deducts **free quota first, then paid credits**, appends a `UsageLedgerEntry` tagged with a `Feature` string (e.g. `"PromptLab:Evaluate"`), and resets free quota monthly.

Both the check and the record run under a **per-user lock** (`IUserUsageLock`), so concurrent completions for the same user (notably the Prompt Lab parallel simulate/evaluate fan-out) cannot race on the shared scoped DbContext or lose a balance update.

> **Remaining interface-honesty gap:** `CheckAndReserveAsync` still only *checks* — it does not hold/reserve tokens before the LLM call. The per-user lock now prevents the DbContext race and lost updates during recording, but two checks can still each pass the gate before either records, so a user can briefly overspend a near-empty balance. True reservation (deduct-on-check with reconcile, or RowVersion-guarded) remains future work; `CreditBalance.RowVersion` exists but is not yet enforced.

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
| Exception mapper Adapter | `{Exception}Mapper` | `AiServiceExceptionMapper` |
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
