# LLM Completion Seam Reshape

**Date:** 2026-06-19
**Type:** refactor
**Environment / Systems:** CodeSmith (.NET 8 API + Infrastructure/Core); branch `refactor/llm-completion-seam`

## TL;DR
Ran an architecture review, chose to collapse the three capability-named LLM interfaces into a single `ILlmService.CompleteAsync` seam, and implemented it. Along the way uncovered and fixed two real DI/concurrency bugs in the usage layer. Builds clean, 203 tests pass (incl. new concurrency regression tests). Not committed.

## Context & Goal
Invoked `/improve-codebase-architecture` to find high-impact deepening opportunities. The codebase had grown well past its docs (PromptLab, SystemLab, usage/credits ledger, 3 LLM providers, 2 code-execution backends). The top finding: the LLM seam was shaped by **caller intent** (7 named methods across `ITutoringLlmService` / `IPromptLabLlmService` / `ISystemLabLlmService`) rather than by the operation, multiplying complexity by provider count. Goal: reshape to one completion operation, improving testability and AI-navigability.

## Key Points Explored
- **The shallow seam:** 7 named methods collapse to 2 real shapes (single-turn + multi-turn completion at a model tier). Forced every provider to implement all 7, declare 3 interfaces, plus 3 near-identical usage decorators and **9 keyed DI registrations**. `XaiLlmService` was a near-verbatim copy of `OpenAiLlmService`.
- **Grilling locked 4 design decisions:** (1) one method + `CompletionRequest.SingleTurn` factory; (2) `ModelTier { Fast, Accurate }` enum (caller picks tier, adapter maps tier→model); (3) `Feature` as a plain `string` (friction-free, matches ledger column); (4) keep the factory but drop the generic (`Get(provider)`), since provider is a runtime value needing keyed-DI resolution.
- **Bug #1 (captive dependency):** the keyed decorators were `AddKeyedSingleton` but depended on scoped `IUsageEnforcer` → scoped `CodeSmithDbContext`. A singleton capturing a scoped DbContext = not thread-safe / scope-validation failure.
- **Bug #2 (DbContext race + lost updates):** PromptLab's `Task.WhenAll` parallel simulate/evaluate fan-out drives concurrent completions → concurrent use of one DbContext and lost balance updates. `UsageEnforcer.CheckAndReserveAsync` also never actually reserved despite its name/claims.

## Decisions & Outcomes
- **Seam reshape shipped:** new `ILlmService`, `CompletionRequest` (record + `SingleTurn`), `ModelTier`; `ILlmServiceFactory.Get(provider)`. `AnthropicLlmService` rewritten; `OpenAiLlmService`+`XaiLlmService` merged into `OpenAiCompatibleLlmService` (xAI = endpoint config); 3 capability interfaces + copy-paste adapter deleted; 3 usage decorators → 1 `UsageEnforcingLlmService`. All 8 call sites migrated; 6 test files migrated to the single `CompleteAsync` mock idiom.
- **Bug #1 fixed:** raw adapters keyed **singletons** (`"raw:{provider}"`); enforcing decorator keyed **scoped**, resolved via the scoped factory → request-scoped enforcer/DbContext. DI went 9 → 3+3 registrations.
- **Bug #2 fixed:** new `IUserUsageLock`/`UserUsageLock` singleton (mirrors `ISystemLabSessionStore.GetLock`); `UsageEnforcer` serializes check + record per user. Chosen over `IDbContextFactory`/`RowVersion` for correctness + pattern consistency (a judgment call, reversible).
- **Verified:** production + tests compile; `dotnet test` = 203 passed, 0 failed. New `UsageEnforcerTests` lost-update test fires 50 parallel deductions through a fake repo modeling the read→write window — fails without the lock, passes with it.
- **Docs updated:** `CONTEXT.md` (full rewrite earlier this thread, then flipped to "Implemented Reshape" + usage caveat) and `README.md` seam row.

## Open Questions / Next Steps
- **Not committed** — awaiting review before commit / PR.
- **Reserve gap remains (deferred):** `CheckAndReserveAsync` still doesn't hold tokens; two concurrent checks can both pass before either records, so a user can briefly overspend a near-empty balance. True reservation (deduct-on-check + reconcile, or enforce `CreditBalance.RowVersion`) is future work.
- Optional: run the API host to confirm DI resolves end-to-end under scope validation.
- Lower-priority architecture candidates surfaced but not taken: structured-output JSON parsing seam (duplicated `ExtractJson`), and the frontend's parallel lab hook/API/type structure.

## Artifacts
- **Branch:** `refactor/llm-completion-seam` (working tree, uncommitted).
- **New files:** `CodeSmith.Core/Enums/ModelTier.cs`, `Core/Models/CompletionRequest.cs`, `Core/Interfaces/ILlmService.cs`, `Core/Interfaces/IUserUsageLock.cs`, `Infrastructure/Services/OpenAiCompatibleLlmService.cs`, `Infrastructure/Services/Usage/UserUsageLock.cs`, `Infrastructure/Services/Usage/Decorators/UsageEnforcingLlmService.cs`, `Tests/Infrastructure/Usage/UsageEnforcerTests.cs`, `Tests/Infrastructure/Usage/UserUsageLockTests.cs`.
- **Deleted:** `ITutoringLlmService.cs`, `IPromptLabLlmService.cs`, `ISystemLabLlmService.cs`, `OpenAiLlmService.cs`, `XaiLlmService.cs`, and the 3 per-capability usage decorators.
- **Key edits:** `ServiceCollectionExtensions.cs` (DI rewrite), `UsageEnforcer.cs` (per-user lock), `LlmServiceFactory.cs`, `AnthropicService.cs`, 8 call sites, `CONTEXT.md`, `README.md`.
- **Verification command:** `dotnet test CodeSmith.Tests/CodeSmith.Tests.csproj` → 203 passed.
