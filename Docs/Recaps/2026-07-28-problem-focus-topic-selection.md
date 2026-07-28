# User-Selectable Problem Focus + Topic (Paired Programmer)

**Date:** 2026-07-28
**Type:** feature
**Environment / Systems:** CodeSmith — `CodeSmith.Core`, `CodeSmith.Infrastructure`, `CodeSmith.Api`, `CodeSmith.Web`, `CodeSmith.Tests`

## TL;DR

The Paired Programmer surface secretly rolled two random variety axes on every problem generation and gave the user no control over either. Both are now user-selectable — **Focus** (kind of work) as a primary control, **Topic** (subject area) behind an Advanced disclosure — each defaulting to a first-class `Random` that preserves the previous behavior exactly. Shipped end to end across 5 phases: 466/466 backend and 160/160 frontend tests green, nothing committed.

## Context & Goal

The user could pick only language and difficulty. Behind that, `TutoringPromptTemplates.cs` held two `string[]` arrays — `ProblemCategories` (12 topic areas) and `ProblemAngles` (10 entries, 8 distinct approach styles) — and rolled one of each with `Random.Shared` into the LLM user message. Neither was visible or controllable.

A terminology correction shaped the whole design: the user's examples ("refactor a codebase", "add a new feature") were **Angles**, not `ProblemCategories`. The two axes were renamed **Focus** and **Topic** because the original names actively misled.

Design was resolved through a `/grill-me` session, written to a plan artifact, reviewed twice, then implemented in one pass.

## Key Points Explored

- **One axis or two.** Initial recommendation was Focus only. The user pushed back; the counter-arguments that survived scrutiny were cell coherence (explicit selection makes all 96 topic×focus cells reachable, including strained ones like `BitManipulation` × `Refactoring`) and variety collapse under double-pinning. The "it's a bigger lift" argument did not survive — the second axis rides identical plumbing, ~1.3× not 2×. Settled on both, asymmetric.
- **`Random` as a domain value, not a null.** `Random = 0` on both enums means `default(TEnum)` deserializes to Random, so the CLI and any older client keep working untouched.
- **Distribution preservation.** `ProblemAngles` listed "Standard implementation" 3× for a ~30% baseline. Going uniform would have silently dropped it to 12.5% for every existing user — rejected as an unannounced retune.
- **Seam shape.** Adding two positional params would have pushed both streaming methods to 8 params, with `focus`/`topic` stranded *after* the callbacks (optional params must be trailing). A `ProblemSpec` record took them from 6 to 4 instead.
- **Naming, twice.** "Real-world domain" (focus) collided with "real-world simulation" (topic). First fix renamed the focus to `AppliedContext` — review showed nobody could interpret it without reading the prose, so it was reversed: focus keeps the plain name `RealWorldScenario` and the *topic* became `SimulationAndModeling`. Also recorded that `RealWorldScenario` is the weakest member of the focus list (the only one describing *framing* rather than *kind of work*) and the first candidate if the list is trimmed.
- **Badge semantics.** A late review caught an apparent contradiction: the hardening prompt authorizes topic drift while the badge asserts the topic as fact. Resolved by narrowing the badge's contract rather than changing either — see below.
- **Randomness testing.** Rejected both an `IRandomSource` seam (only two adapters would exist) and a 10,000-iteration statistical test (slow, inherently flaky). The weighted roll is asserted as static data.

## Decisions & Outcomes

| Decision | Resolution |
|---|---|
| Axes exposed | Both, asymmetric — Focus always visible, Topic behind `<details>` defaulting to Random |
| `Random` modeling | `Random = 0` first enum member; **never reorder these enums** |
| Distribution | `WeightedFocusRoll` holds `Standard` ×3 — the 30%/10% split survives unchanged |
| Seam | `ProblemSpec(Difficulty, Language, Provider, Focus, Topic)` through all four interfaces |
| Generator return | `GeneratedProblem` record replaces the `(string, string)` tuple |
| `ProblemGenerationRequest` | `Category`/`Angle` strings **replaced** by enums, not supplemented |
| Badge contract | **Request fidelity, not output fidelity** — reports what was sent to the provider, makes no claim about what the model delivered |
| `Standard` badge | Renders like any other focus — it's a real option, not the absence of one |
| Stickiness | Selection survives the in-app nav reset, resets on page reload |
| Regenerate | Sends the **selection**, never `session.focus` |
| Bad cells | Prompt hardening only — focus binding, topic bendable. No blocklist |
| CLI | Out of scope; compiles and behaves identically, untouched |

**Verification:** clean `dotnet build`, clean `npx tsc --noEmit`, 466/466 backend, 160/160 frontend. The 4 remaining ESLint errors are pre-existing in `NavigationContext.tsx`, `TerminalPanel.tsx`, and `useProviderPreference.ts` — none appear in the diff.

**Incidental bug found and fixed:** `ChatWindow.test.tsx` used only `vi.restoreAllMocks()` in `beforeEach`, which does **not** clear `mock.calls`. Call history leaked across tests, so `mock.calls[0]` assertions were reading an earlier test's call and passing on the wrong data. Added `vi.clearAllMocks()`. Any pre-existing assertion on `mock.calls[n]` in that file was relying on being first to run.

**Two deliberate deviations from the plan:**
- `language` state was **not** lifted out of `DifficultySelector`. The plan called for it, but the stated justification (F9) covers only focus/topic; lifting language would make it persist across the nav reset — an unrequested behavior change.
- `ProblemGenerator`'s log line uses the enums directly rather than deriving prose — same information, better shaped for structured queries in App Insights.

## Open Questions / Next Steps

- Nothing committed — working tree only, 21 modified + 5 new files.
- `ChatWindow.tsx:216-218` uses `//` comments between JSX attributes. Valid TSX (`{/* */}` is not legal in that position) and both typecheck and lint pass, but unusual enough that the user may prefer them moved above the element.
- Watch whether `RealWorldScenario` earns its slot once real output is observed — the system prompt already instructs the model to embed problems in real-world context on every generation, so it partly duplicates baseline behavior.
- Strained topic×focus cells are mitigated by prompt hardening, not eliminated. If contrived problems show up in practice, the blocklist option is documented in the plan.

## Artifacts

**New:** `CodeSmith.Core/Enums/ProblemFocus.cs`, `ProblemTopic.cs`, `CodeSmith.Core/Models/ProblemSpec.cs`, `GeneratedProblem.cs`, `CodeSmith.Tests/Core/ProblemSpecTests.cs`

**Modified (backend):** `ITutoringPromptTemplates.cs`, `IProblemGenerator.cs`, `ITutoringService.cs`, `ProblemSession.cs`, `TutoringPromptTemplates.cs`, `ProblemGenerator.cs`, `TutoringService.cs`, `SessionController.cs`, `CreateSessionRequest.cs` + 4 test files

**Modified (frontend):** `types.ts`, `DifficultySelector.tsx`, `ChatWindow.tsx` + 3 test files

**Docs:** `context.md` — new "Problem Variety (Focus + Topic)" subsection, Key Models rows, Seams table, and 5 new Ubiquitous Language terms (**Focus**, **Topic**, **Resolved**, **Selection**, **Weighted Focus Roll**). `CLAUDE.md` — `POST /api/session` request/response shape.

**Plan:** `~/.claude/plans/codesmith-problem-focus-topic-selection.md` — 4 revisions, 30 named tests, full decision log with alternatives considered.
