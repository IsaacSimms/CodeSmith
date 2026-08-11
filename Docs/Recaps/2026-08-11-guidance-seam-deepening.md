# Guidance Seam Deepening (Backend Frame + Frontend State Machine)

**Date:** 2026-08-11
**Type:** refactor
**Environment / Systems:** CodeSmith (.NET 8 backend — Core/Infrastructure/Tests; React 19 SPA — CodeSmith.Web)

## TL;DR

Ran `/improve-codebase-architecture`, surfaced 10 deepening candidates, and implemented the top two: the backend guidance-turn frame (lock/load/dispatch/persist) folded behind `IGuidanceConversation` via a new generic session-level entry point, and the frontend guidance chat state machine unified into one shared `useGuidanceChat` hook with the three surface hooks reduced to thin adapters. TDD both sides; backend 508/508, frontend 295/295, tsc clean.

## Context & Goal

User requested an architecture review pass. An Explore agent walked the codebase against the project's Ubiquitous Language (Module/Interface/Seam/Depth/Locality), excluding settled decisions (LLM Completion Seam, LlmJson, session eviction, RowVersion, streaming push-callback shape). Ten ranked candidates were presented; user picked #1 and #2 for implementation.

## Key Points Explored

Candidates found (implemented in **bold**):

1. **Backend guidance-turn frame written three times** — `TutoringService`, `PromptLabService`, `SystemLabService` each repeated a ~40-line frame around `IGuidanceConversation`: blocking+streaming wrapper pair, per-session lock, load-or-throw, the `onDelta is null ? RunTurnAsync : StreamTurnAsync` dispatch (verbatim ×3), `ChatHistoryWindow = 20` in triplicate.
2. **Frontend guidance-turn state machine copied across three windows** — optimistic append / `onError` rollback / partial snapshot / `failedTurn` / draft restore, character-for-character in `ChatWindow`, `PromptLabWindow`, `SystemLabWindow`; the settle-invalidation rule (`invalidateAccountUsageQueries`) pasted into eight hooks; none of the mutation hooks tested.
3. NDJSON endpoint lifecycle leaks across the `NdjsonStreamWriter` seam (byte-identical try/catch in four controller actions; blocking/stream 400-message drift already present).
4. Domain-exception passthrough enforced in three drifting shapes (3-type vs 5-type lists; `AggregateException` flattening only in PromptLab).
5. Enum wire contracts hand-mirrored in four places; `isAiProvider` hardcodes the provider union the server already publishes.
6. Tier-downgrade policy as substring sniffing (`feature.Contains("Evaluate")`) in the usage decorator.
7. `CodeSmith.CLI` functionally dead — sends no auth, all its endpoints are `[MeteredAi]` → 401 always; fails the deletion test.
8. Executor: 310 untested lines behind a hand-mirrored wire contract.
9. Prompt-template asymmetry (labs build prompts inline; tutoring has a tested seam); `TutoringService` had no direct tests.
10. `InMemorySessionStore<T>.Set` switches on concrete session types.

## Decisions & Outcomes

### Candidate 1 — backend (shipped)

- New `IGuidanceSession` contract in Core (`Provider` + `GuidanceHistory`), implemented **explicitly** on `ProblemSession` / `PromptLabSession` / `SystemLabSession` so the alias never appears in API serialization.
- `IGuidanceConversation` reshaped to one generic entry: `RunTurnAsync<TSession>(store, sessionId, buildTurn, onDelta?, ct)`. The Module now owns lock → load-or-throw `SessionNotFoundException` → `buildTurn` → append/trim/complete → persist → whole-turn rollback. `onDelta` selects the streaming shape; `buildTurn` failures (catalog lookups) propagate unwrapped and mutate nothing (pinned by test). Old `RunTurnAsync`/`StreamTurnAsync` pair deleted.
- Triplicated window constant became `GuidanceTurnRequest.DefaultHistoryWindow` (20); `MaxTurns` now defaulted.
- Orchestrator chat paths reduced to a `buildTurn` lambda + projection. Lock semantics preserved: submits still take the same store's per-id semaphore in the orchestrators, so submit-vs-chat serialization is unchanged (existing 8-way concurrency alternation test passes against real modules).
- Orchestrator tests migrated from mocking `IGuidanceConversation` call shapes to running the real `GuidanceConversation` over a substituted `ILlmService` — behavior asserted through each surface's own Interface.

### Candidate 2 — frontend (shipped)

- New `useGuidanceChat` (`src/hooks/`, 8 behavioral tests): owns messages, optimistic append, rollback + partial snapshot (partial omitted when nothing streamed — the framing rule), draft restore, streaming text reset, settle invalidation of quota/balance/ledger. Owns the `FailedTurn` type; `StreamingChatTail` re-exports it.
- `useSendMessage` / `usePromptLabChat` / `useSystemLabChat` rewritten as thin adapters: message shape + `stream*` apiClient call; tutoring's also absorbed context-token telemetry (`resetContextUsage` for nav reset). Each exposes `sendTurn(...)`; rejection swallowed since failure surfaces via `failedTurn` state (matches prior `mutate`-with-callbacks behavior — flagged to user).
- Three windows each lost ~35 lines; `handleSendChat` is one line. Window tests passed **unchanged** — they assert rendered behavior.
- `turnSettleInvalidation.test.tsx` migrated from `mutateAsync` to `sendTurn`.

### Verification

- `dotnet test` → **508 passed, 0 failed**.
- `npx vitest run` → **295 passed** (37 files); `npx tsc --noEmit` clean.
- Every slice red→green (session-level tests failed on `NotImplementedException` before the implementation landed).

### Docs

`context.md` reconciled: Seams table row for the guidance turn, session-serialization section (chat locks in the Module, submits in orchestrators, same semaphore), Tutoring/System Lab subsystem text, **Guidance Turn** UL entry rewritten + new **IGuidanceSession** entry, frontend conventions (known three-surface duplication now scoped to start/submit only).

## Open Questions / Next Steps

- Candidates 3–10 remain unimplemented; highest-value next per review: NDJSON endpoint executor (#3) and the domain-exception passthrough marker (#4). CLI deletion (#7) is a fast decision with doc impact (`context.md` cites the CLI as the reason blocking endpoints exist).
- check-work verification offered, not yet run (user may waive).
- Nothing committed — all changes are uncommitted in the working tree.

## Artifacts

| Path | State |
|------|-------|
| `CodeSmith.Core/Interfaces/IGuidanceSession.cs` | new |
| `CodeSmith.Core/Interfaces/IGuidanceConversation.cs` | reshaped (one generic method) |
| `CodeSmith.Core/Models/GuidanceTurnRequest.cs` | `DefaultHistoryWindow` added |
| `CodeSmith.Core/Models/{ProblemSession,PromptLab/PromptLabSession,SystemLab/SystemLabSession}.cs` | implement `IGuidanceSession` explicitly |
| `CodeSmith.Infrastructure/Services/GuidanceConversation.cs` | owns full turn frame |
| `CodeSmith.Infrastructure/Services/{TutoringService,PromptLab/PromptLabService,SystemLab/SystemLabService}.cs` | frames deleted |
| `CodeSmith.Tests/Infrastructure/GuidanceConversationTests.cs` | rewritten at session level (12 tests) |
| `CodeSmith.Tests/Infrastructure/{AnthropicServiceTests,PromptLab/PromptLabServiceTests,SystemLab/SystemLabServiceTests}.cs` | migrated to real-GuidanceConversation idiom |
| `CodeSmith.Web/src/hooks/useGuidanceChat.ts` + `.test.tsx` | new (8 tests) |
| `CodeSmith.Web/src/features/{chat/hooks/useSendMessage,prompt-lab/hooks/usePromptLabChat,system-lab/hooks/useSystemLabChat}.ts` | rewritten as adapters |
| `CodeSmith.Web/src/features/{chat/components/ChatWindow,prompt-lab/components/PromptLabWindow,system-lab/components/SystemLabWindow}.tsx` | state machines removed |
| `CodeSmith.Web/src/features/chat/components/StreamingChatTail.tsx` | re-exports `FailedTurn` |
| `CodeSmith.Web/src/features/account/hooks/turnSettleInvalidation.test.tsx` | migrated to `sendTurn` |
| `context.md` | reconciled to the new seam shape |

Verify commands: `dotnet test CodeSmith.Tests/CodeSmith.Tests.csproj` · `cd CodeSmith.Web && npx vitest run && npx tsc --noEmit`
