# LlmJson Parsing Module — Deepening Refactor + SystemLabEvaluator Tests

**Date:** 2026-07-07
**Type:** refactor
**Environment / Systems:** CodeSmith backend (.NET 8) — `CodeSmith.Infrastructure`, `CodeSmith.Tests`

## TL;DR
Ran `/improve-codebase-architecture`, picked candidate 1 (unify LLM-response JSON parsing), and grilled the design to a locked contract. Implemented it TDD, characterize-donor-first: new `LlmJson` static Module now owns fence-stripping, one failure mode (`EvaluationParseException`), and the single rubric-integrity walk; three scoring-integrity holes closed along the way. 324/324 backend tests green (up from 276).

## Context & Goal
An architecture review surfaced seven deepening candidates; the user chose the LLM-response parsing consolidation plus unit tests for `SystemLabEvaluator` (the most complex parser in the repo, previously **zero** tests). The friction: `ExtractJson` was copy-pasted verbatim ×3, `BuildUserMessage` ×2, and three modules used three different JSON parse strategies with three different failure behaviors.

## Key Points Explored
Design was settled in a grill-me loop; locked decisions:
- **Scope: full** — shared parse + unified error mode + one criterion-score walk (not just dedup).
- **Shape: `internal static class` in Infrastructure**, deliberately *not* a DI Seam — pure functions, one adapter would be a hypothetical Seam, and mockability would let evaluator tests bypass the real parse path. `InternalsVisibleTo("CodeSmith.Tests")` already existed.
- **Error mode: reuse `EvaluationParseException`** (no new type, no HTTP behavior change — SystemLabService's `catch…when` re-wraps it to `AiServiceException`/502; PromptLab's generator failures fall back to static inputs).
- **Walk semantics: tolerant SystemLab donor semantics** — skip missing/hallucinated criterion IDs, points as double→round, missing points → 0, clamp `[0, MaxPoints]`.
- **Surface: minimal 4 methods** (`ExtractJson`, `Parse`, `Deserialize<T>`, `ParseCriterionScores`); no accessor helpers.
- `BuildUserMessage` dedup → new PromptLab-local `TestInputMessage` static helper.

Bugs found during design (all fixed):
1. **PromptLab phantom points** — `PromptEvaluator` kept hallucinated criterion IDs with whatever points the model invented (`MaxPoints = 0` but `Points` counted toward `TotalScore`).
2. **PromptLab fragile points parsing** — `GetInt32()` meant a fractional score (`"points": 8.5`) or missing field failed the *entire* per-input result ("Could not parse").
3. **SystemLab unclamped invented penalties** — `ParseDimensionDeductions` accepted hallucinated dimension names with `maxDed = deduction`, so the model could subtract arbitrary points via a nonexistent dimension.

## Decisions & Outcomes
Executed in the agreed characterize-donor-first TDD order:
1. `SystemLabEvaluatorTests` (21 tests) written against current code — 20 green pinning donor behavior, 1 deliberate red (dimension-skip) → one-line fix in `ParseDimensionDeductions` (`if (dim is null) continue;`).
2. `LlmJsonTests` (19) red → `LlmJson.cs` implemented → green.
3. `SystemLabEvaluator` rewired onto `LlmJson`; step-1 suite stayed green, proving the extraction changed nothing. Unused logger removed from its ctor.
4. `PromptEvaluatorTests` +5 integrity tests (red against old code) → `ParseResult` rewired → green. PromptLab scoring now clamps, drops hallucinated IDs, tolerates fractional/missing points.
5. `TestInputGenerator` → `LlmJson.Deserialize` (malformed JSON now `EvaluationParseException`; count≠4 check stays caller-side); `PromptSimulator`/`PromptEvaluator` → `TestInputMessage.Build`; `TestInputMessageTests` (4).
6. `context.md` updated: `LlmJson` + `TestInputMessage` added to Ubiquitous Language; Prompt Lab / System Lab subsystem sections note the shared Module and the "neither invent points nor invent penalties" invariant.

Verification: full suite `dotnet test CodeSmith.Tests` → **324/324 passed**. ~90 lines of duplication deleted outside the new module. Left uncommitted (tree also holds concurrent Stripe billing work by another session).

## Open Questions / Next Steps
- Commit pending — should be separate from the uncommitted Stripe billing changes sharing the tree.
- Remaining review candidates (not started): session-turn envelope in the three orchestrators, frontend surface chassis (system-lab has zero tests), exception-mapper table (now 9 one-off mappers after billing added two), spend-policy locality, provider onboarding, session-store generic leak.
- `EvaluationParseException` is still unmapped in the exception→HTTP table (effectively 502 via re-wrap on the SystemLab path) — unchanged, by design.

## Artifacts
- New: `CodeSmith.Infrastructure/Services/LlmJson.cs`, `CodeSmith.Infrastructure/Services/PromptLab/TestInputMessage.cs`
- New tests: `CodeSmith.Tests/Infrastructure/SystemLab/SystemLabEvaluatorTests.cs`, `CodeSmith.Tests/Infrastructure/LlmJsonTests.cs`, `CodeSmith.Tests/Infrastructure/PromptLab/TestInputMessageTests.cs`
- Edited: `SystemLabEvaluator.cs`, `PromptEvaluator.cs`, `TestInputGenerator.cs`, `PromptSimulator.cs`, `PromptEvaluatorTests.cs`, `TestInputGeneratorTests.cs`, `context.md`
