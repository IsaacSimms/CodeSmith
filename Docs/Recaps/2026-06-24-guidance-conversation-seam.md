# Guidance Conversation Seam — Unifying the Three Surfaces' Chat Flow

**Date:** 2026-06-24
**Type:** refactor
**Environment / Systems:** CodeSmith backend (.NET 8 — Core / Infrastructure / Api), xUnit + NSubstitute

## TL;DR
Ran an architecture review, picked the top candidate, grilled the design, and implemented it TDD-first: a single deep `IGuidanceConversation` Module that owns the multi-turn guidance-chat invariant previously hand-copied (and divergent) across `TutoringService`, `PromptLabService`, and `SystemLabService`. Fixed four real/latent bugs in the process. Full backend suite green at 215 passed / 0 failed. Nothing committed — working tree only.

## Context & Goal
Invoked `/improve-codebase-architecture` to surface deepening opportunities. Four candidates were presented; the user chose **#1 — unify the three surfaces' guidance-chat turn**. All three orchestrators reimplemented the same dance (append user → build Socratic prompt → `CompleteAsync(Fast)` → append assistant → persist → roll back on failure), and the copies had diverged. Goal: concentrate that invariant in one deep Module (Locality), fixing the divergences.

## Key Points Explored
Grilling loop resolved the design via numbered forks:
- **A** — System prompt passed as *data* (pre-built string); each surface keeps its own builder. The Module never learns about `Challenge`/`Scenario`/`ProblemSession`.
- **B** — Module mutates a `List<ChatMessage> history` + calls a `persist` delegate; decoupled from the three session types.
- **C** — The SystemLab per-session `SemaphoreSlim` stays in the orchestrator (it also guards `SubmitAttemptAsync`, so it's broader than a chat turn).
- **D** — Module returns the full `LlmResponse`; callers project (Tutoring → `ChatResponse` with token info; labs take `.Content`).
- **E** — Trimming was a bug (only PromptLab trimmed; Tutoring/SystemLab grew unbounded). All three now trim.
- **F** — Trim must drop *whole turns* anchored on a User message; the old `RemoveAt(0)` loop could leave a window starting on an Assistant message, which Anthropic rejects.
- **G** — `ModelTier.Fast` baked into the Module (an invariant, not a per-call choice); makes a future Fast→Accurate switch a one-line change in one place.
- **H2** — Turn-shaped data bundled into a `GuidanceTurnRequest` parameter object (mirrors `CompletionRequest`); `provider`/`history`/`persist` stay as separate session-wiring args. A `Conversation` binding type was *declined* per the project's "two adapters = real seam" rule — only one (in-memory) persistence backend exists.
- **J** — Module owns uniform error-wrapping (`AiServiceException`) alongside rollback; `AiServiceException` and `OperationCanceledException` pass through untouched.

## Decisions & Outcomes
Implemented TDD-first (tests written before the implementation):

**Bugs fixed (the candidate's whole justification):**
1. Tutoring no longer leaks a dangling user turn on LLM failure (it had no rollback before).
2. Uniform error mapping — Tutoring used to surface a raw 500 while the labs returned 502; all three now 502.
3. Trim no longer breaks the user-first invariant (whole-turn anchoring) — this latent bug would have hit all three the moment trimming turned on everywhere.
4. Cancellation now maps to 499, not 502 — the old lab `catch` filters wrapped `OperationCanceledException`.

**Verification:** `dotnet test CodeSmith.Tests` → **215 passed, 0 failed, 0 skipped**. New `GuidanceConversationTests` (8 tests) is the test surface for the moved behavior; the three surfaces' chat tests collapsed to thin delegation tests, and the moved mechanics tests were deleted rather than layered (per project convention).

## Open Questions / Next Steps
- **Not committed** — changes live in the working tree for line-by-line review.
- Three remaining architecture candidates from the review are unstarted: #2 consolidate the structured-JSON-completion pattern (`ExtractJson` duplicated 3×, divergent malformed-JSON handling); #3 give the usage accounting rules a single owner + stop the `Feature`-string tier-downgrade sniff; #4 remove the type-switch from `InMemorySessionStore`. Also noted: frontend's parallel start/submit/chat hook-API-type duplication (lower priority).
- A future Fast→Accurate switch for guidance is now a one-line change in `GuidanceConversation`.

## Artifacts
**Created:**
- `CodeSmith.Core/Interfaces/IGuidanceConversation.cs` — the seam.
- `CodeSmith.Core/Models/GuidanceTurnRequest.cs` — turn data (`SystemPrompt`, `UserMessage`, `MaxTokens`, `MaxTurns`, `Feature`; no `Tier`).
- `CodeSmith.Infrastructure/Services/GuidanceConversation.cs` — the implementation.
- `CodeSmith.Tests/Infrastructure/GuidanceConversationTests.cs` — 8 tests.

**Modified:**
- `CodeSmith.Infrastructure/Services/TutoringService.cs`, `Services/PromptLab/PromptLabService.cs`, `Services/SystemLab/SystemLabService.cs` — refactored onto the seam; dropped `ILlmServiceFactory` from chat paths and the hand-rolled append/trim/rollback.
- `CodeSmith.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs` — registered `IGuidanceConversation` scoped.
- `CodeSmith.Tests/Infrastructure/AnthropicServiceTests.cs` (contains `TutoringServiceTests`), `.../PromptLab/PromptLabServiceTests.cs`, `.../SystemLab/SystemLabServiceTests.cs` — rewired to the new constructor + thin delegation tests.
- `context.md` — added Guidance Conversation / Guidance Turn glossary terms, a Seams-table row, and updated the three subsystem blurbs.
