---
id: 010
title: Resolve the request provider from AiOptions.ActiveProvider
type: grilling
status: closed
blocked_by: []
---

## Question

`AiOptions.ActiveProvider` is **never used to resolve a request's provider**. It is read in exactly
one place — `SessionController.cs:42` — to *tell the client* what the default is. The server never
applies it. Every request path hardcodes Anthropic:

| Path | How it lands on Anthropic |
|------|---------------------------|
| `CreateSessionRequest.cs:19` | Non-nullable `AiProvider`; an omitted field binds to the zero member, `Anthropic` |
| `StartSystemLabSessionRequest.cs:9` | Explicit `= AiProvider.Anthropic` initializer |
| `PromptLabController.cs:62` | `request.Provider ?? AiProvider.Anthropic` — already nullable, still hardcodes |

So with `AiOptions.ActiveProvider = "Xai"` (`AiOptions.cs:11`), the configured default is advisory
only. This is the server-side twin of the client defect settled in
[Design the provider-preference context](004-design-provider-preference-context.md).

**It is already shipped and live, independent of the SPA.** `CodeSmith.CLI/Services/ApiClient.cs:33`
posts `new { difficulty, language }` with no provider at all — every CLI session has always run on
Anthropic while the configured active provider was xAI.

**This blocks ticket 004 decision 4**, whose bounded fallback ungates Start after ~3s and omits
`provider` so the server applies `ActiveProvider`. Today that fallback silently yields Anthropic,
which is the exact outcome 004 exists to eliminate.

Resolve:

- **Where does resolution live?** A DTO default cannot do it — property initializers have no access
  to `IOptions<AiOptions>`. Controller, service, or a model binder? Note that scattering
  `?? _aiOptions.ActiveProvider` across three controllers reproduces the duplication that produced
  this defect in the first place.
- **Do all three surfaces resolve identically**, or does PromptLab's existing `??`
  (`PromptLabController.cs:62`) stay special?
- **Does the response echo the resolved provider?** `ProblemSession` already echoes *resolved* focus
  and topic rather than the requested `Random` (see map → Problem Variety). A client that omits
  `provider` currently has no way to learn what it got. `PromptLabSessionResponse.cs:15` carries a
  `Provider` field defaulted to Anthropic; `ProblemSession` needs checking.
- **Do the request DTOs become nullable?** `CreateSessionRequest.Provider` → `AiProvider?` and
  `StartSystemLabSessionRequest.Provider` loses its initializer — or is there a non-breaking shape?
- **What happens to the `Enum.IsDefined` guards** at `SessionController.cs:71` and `:106`? With a
  nullable field, a null must pass validation while a garbage value must still 400.
- **Should `ActiveProvider` stop being a raw `string`?** It is never parsed against `AiProvider`
  anywhere. A typo in config currently ships garbage to the client via `GetProviders()` and would
  fail resolution at request time rather than at startup. The codebase already has a fail-fast
  startup validation pattern for provider options
  (`ServiceCollectionExtensions.cs:225` `AddValidatedProviderOptions`).
- **Does the CLI change?** It could keep omitting `provider` and inherit the corrected default, or
  gain an explicit flag. Omitting is the smaller change and is arguably the correct behavior for a
  client with no provider UI.
- **What happens to `SessionControllerTests.cs:120`**, which currently pins
  `s.Provider == AiProvider.Anthropic` for a request that omits the field? It encodes the defect as
  expected behavior.

## Answer

**`AiOptions.ActiveProvider` becomes binding.** It is retyped from `string` to `AiProvider` and
validated at startup, and a single sealed `AiProviderResolver` turns "the client said nothing" into
that value at all four LLM-creating endpoints. The three request DTOs converge on `AiProvider?` so
omission is expressible rather than indistinguishable from a zero-valued enum; an undefined value
throws through the existing middleware table instead of being guarded on one surface and unguarded
on two. Every surface echoes the resolved provider back, as `ProblemSession` already does for focus
and topic.

### Decisions

| # | Decision | Reasoning |
|---|----------|-----------|
| 1 | **`AiOptions.ActiveProvider` is retyped `string` → `AiProvider`**, bound with `.ValidateOnStart()` | The config binder converts `"Xai"` case-insensitively and throws at host start on a typo, so garbage can never reach `GetProviders()` or a request. `Program.cs:39` registers a global `JsonStringEnumConverter`, so the field still serializes as `"Xai"` — no wire break, `SessionControllerTests.cs:29` unaffected. Resolution becomes assignment: the request-time parse failure mode ceases to exist rather than relocating. Sits inside 009's precedent of renaming live config keys with no compat path |
| 2 | **One resolver Module**, called by all three controllers | Deletion test: remove it and `?? _aiOptions.ActiveProvider` reappears in three controllers — exactly the duplication that produced this defect. Rejected a **model binder** (resolution becomes invisible and "omitted" vs "explicitly chose" is lost forever), pushing into the **three services** (three independent resolutions, plus `AiProvider?` forced through Core interfaces), and resolving inside **`ILlmServiceFactory.Get`** (the resolved value must be persisted on the session and echoed; resolving at factory time leaves the stored value null) |
| 3 | **`AiProvider?` on all three request DTOs** | `CreateSessionRequest.Provider` goes nullable; `StartSystemLabSessionRequest` drops its `= AiProvider.Anthropic` initializer; `StartChallengeRequest` already is. Non-breaking on the wire (omitted → null) and in tests (`Provider = AiProvider.Anthropic` still compiles). This is what makes "do all three resolve identically" a **yes** — PromptLab loses its `??` at `:62` and stops being special. `SessionController.ToSpec` (`:48`) stops being `static` on the request alone and takes the resolved provider as a parameter |
| 4 | **The resolver throws `UnknownProviderException`; one row in the middleware table maps it to 400** | All three surfaces get identical validation because all three already call the resolver, and `SessionController`'s two `Enum.IsDefined` provider guards (`:71`, `:106`) delete. Uses the codebase's own documented extension point — `ExceptionHandlingMiddleware.cs:22-33` states "adding a new exception type is one table row", with `InvalidPriceException → 400` as the exact precedent for rejecting an allow-list failure. Fixes a live inconsistency: today `(AiProvider)999` is a 400 on Tutoring and a **500** on Prompt Lab and System Lab, because those controllers guard nothing and the value reaches `ILlmServiceFactory.Get`. Rejected `[EnumDataType]` (emits `ValidationProblemDetails`, diverging from both existing shapes) and duplicating hand-rolled guards into four places |
| 5 | **All three responses echo the resolved provider** — `SystemLabSessionResponse` gains `Provider` (field + `FromSession` mapping) | `ProblemSession.cs:14` and `PromptLabSessionResponse.cs:15` already carry it; System Lab was the only surface where an omitting client stayed permanently blind, despite `SystemLabSession.cs:11` holding the value. Matches the map's established precedent that resolved focus and topic are echoed rather than the requested `Random`. Tutoring's NDJSON `final` event carries the whole `ProblemSession`, so the stream path gets it for free |
| 6 | **Drop the `AiProvider provider = AiProvider.Anthropic` default parameter from `IPromptLabService.StartChallengeAsync`**; leave the model and DTO initializers | Of the five surviving hardcodes, the Core-interface default is the only one that can silently supply Anthropic to a real caller — `StartChallengeAsync("id")` compiles and runs. The initializers on `PromptLabSession.cs:14`, `SystemLabSession.cs:11`, and `PromptLabSessionResponse.cs:15` are always overwritten, and deleting them only exposes the implicit zero, which is still `Anthropic`. Cosmetic, not a fix. Rejected making `Provider` `required` on the session models: it churns ~10 object-initializer sites and makes `ProblemSession` fail deserialization when the field is absent, which the CLI does over the wire (`ApiClient.cs:38`) |
| 7 | **The CLI does not change** — it keeps omitting `provider` and inherits the corrected default | Omission is the correct wire meaning for a client with no provider UI: "server decides" is now true rather than a lie. **Recorded as a conscious live behavior change** — every CLI session flips Anthropic → xAI, on the same pre-production reasoning as 009 decision 1. Rejected a `--provider` flag (new surface, validation, and tests on a client with no provider concept) |
| 8 | **The resolver is a sealed concrete class, not an interface** — `AiProviderResolver(IOptions<AiOptions>)` exposing `AiProvider Resolve(AiProvider? requested)` | It is a pure function of configuration: no I/O, no nondeterminism, no credible second implementation, no compile-time dependency to invert — none of the three things that make an interface earn its keep. The decisive argument is reliability, not philosophy: an `IAiProviderResolver` invites `Substitute.For<>` in three controller suites that already mock heavily, and **mocking the applier is precisely how "the configured value never reaches a request" survives a green suite**. A concrete class leaves controller tests no sane option but to construct the real one from real options, so every one of them exercises the actual rule. Deliberately departs from the codebase's interface-per-service convention. Considered and rejected a `ResolvedProvider` wrapper type for compile-time enforcement: C# cannot enforce the smart constructor across assembly boundaries unless the resolver ships in the same assembly as the type, so the guarantee degrades to documentation while still churning ~8 `ProblemSpec` construction sites |
| 9 | **`GetProviders()` keeps its shape; the omit→`activeProvider` rule is documented** | The field stops being advice and becomes the answer to "what do I get if I omit `provider`". [004](004-design-provider-preference-context.md) decision 7 rests on that guarantee, so it gets written down rather than assumed. Rejected renaming to `defaultProvider` (breaks `ProvidersResponse` and `SessionControllerTests.cs:34` for cosmetic gain) and filtering `availableProviders` (004 already investigated and closed it as cosmetic) |
| 10 | **Test set: resolver units + four omission tests + one migration + one middleware row** | ① `AiProviderResolverTests` — omitted → `ActiveProvider`; explicit honored *even when it differs from* `ActiveProvider`; undefined throws. ② **One omission test per creation endpoint, all four** (`CreateSession`, `CreateSessionStream`, `StartChallenge`, `StartSession`): configure `ActiveProvider = Xai`, send no `provider`, assert the service received `Xai`. This layer is load-bearing — it is the test that would have caught the original bug and the only thing that catches a fifth endpoint forgetting; including the streaming sibling matters most, because duplicated validation is where a half-applied fix hides. ③ Migrate `SessionControllerTests.cs:89`. ④ `UnknownProviderException → 400` in `ExceptionHandlingMiddlewareTests`. TDD-shaped: the four omission tests fail red against today's paths. Rejected end-to-end integration coverage — no `WebApplicationFactory` harness exists, so it would mean building test infrastructure inside this ticket |
| 11 | **Docs radius: live product surface only** | `context.md`, `README.md:217`, `CLAUDE.md`'s CreateSession contract (`provider` becomes optional, with the omit→`activeProvider` rule stated), and `AiOptions.cs:11`'s comment — whose "Must match an `AiProvider` enum value name" instruction is now enforced by the type rather than requested of the reader. `Docs/Recaps/2026-06-28-xai-default-pricing-markup.md` stays untouched; it correctly records that `ActiveProvider` was cosmetic *at the time it was written*. 009 decision 11 |

### Placement

- `AiProviderResolver` → `CodeSmith.Infrastructure/Configuration/`, registered beside
  `services.Configure<AiOptions>(...)` at `ServiceCollectionExtensions.cs:38` so config knowledge
  stays in one file.
- `UnknownProviderException` → `CodeSmith.Core/Exceptions/`, where every other type in the
  middleware mapping table lives.
- `AiProviderResolverTests` → `CodeSmith.Tests/Infrastructure/`, mirroring the project under test.

### Codebase facts that shaped this

- **Chat needs no resolution.** Provider locks at session creation and every subsequent turn reads
  `session.Provider`; the chat request DTOs carry no provider at all. The resolver has exactly four
  call sites, not seven.
- **The Question names the wrong test.** `SessionControllerTests.cs:120` does *not* encode the
  defect — line 115 sets `Provider = AiProvider.Anthropic` explicitly, making it a language-forwarding
  theory that survives untouched. No test currently pins the omitted-field behavior at all, which is
  its own gap and is what decision 10 ② fills. The test that genuinely breaks is
  `SessionControllerTests.cs:89` (`CreateSession_WithInvalidProvider_Returns400`), which asserts
  `BadRequestObjectResult` and must become `Assert.ThrowsAsync<UnknownProviderException>` — the
  pattern `PromptLabControllerTests.cs:118-126` already uses for `ChallengeNotFoundException`.
- **The Question's echo bullet is half wrong.** `ProblemSession.cs:14` already carries `Provider`, so
  Tutoring echoes today. `SystemLabSessionResponse` is the real gap — it maps only `SessionId`,
  `ScenarioId`, `Attempts`, and `CreatedAt`.
- **A sentinel enum member would corrupt data.** `CodeSmithDbContext.cs:36` explicitly converts
  `Type` but not `Provider`, so EF persists `AiProvider` as `int`. Adding an `AiProvider.Default = 0`
  member shifts `Anthropic 0→1`, `OpenAi 1→2`, `Xai 2→3` and silently rewrites the meaning of every
  stored `UsageLedgerEntry.Provider`.
- **Only numeric garbage reaches the guard.** `JsonStringEnumConverter` reads numbers as well as
  names, so `"provider": "Grok"` already 400s at model binding while `"provider": 999` binds
  successfully — that is the case the `Enum.IsDefined` guard exists for, and the case that 500s on
  Prompt Lab and System Lab today.
- **Stream ordering is a constraint, not a preference.** The resolver call sits with the other
  validations, **before** `new NdjsonStreamWriter(...)`. The comment at `SessionController.cs:101`
  ("these run before any write, so they keep real 400s") protects exactly this: throw after the
  writer exists and the status line is frozen.
- **A fifth hardcode exists beyond the three the Question lists** —
  `IPromptLabService.StartChallengeAsync`'s default parameter, in a Core interface.

### Consequences for the map

- **Unblocks [004](004-design-provider-preference-context.md) decision 7.** Its bounded ~3s fallback
  now genuinely means "we asked the server to decide" rather than "we silently guessed Anthropic",
  which is the whole basis on which omission was accepted as a fallback but rejected as the primary
  path.
- **Causes one edit inside 004's diff.** The SPA's `CreateSessionRequest` TypeScript type carries
  `provider` as required and `apiClient.test.ts` sends it on every call; the omit-fallback needs it
  optional.
- **No implementation-order coupling.** 009 → 008 are chained because both edit `SettleAsync`; this
  ticket touches controllers, request DTOs, and config. It lands independently, in any order.
- **Closes the frontier.** With 009 and 010 resolved the map has no open grilling tickets — every
  remaining item is implementation.
- **No new tickets.**
