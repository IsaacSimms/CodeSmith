---
id: 005
title: Make AiOptions.ActiveProvider binding at request time
status: done
implements: [010]
depends_on: []
---

## Goal

Turn `AiOptions.ActiveProvider` from advisory config into the value a request actually gets when the
client omits `provider`, applied identically at all four LLM-creating endpoints.

## Constraints

- Retype `AiOptions.ActiveProvider` `string` → `AiProvider`, bound with `.ValidateOnStart()` —
  [Resolve the request provider from AiOptions.ActiveProvider](../tickets/010-resolve-request-provider-from-active-provider.md) #1
- One `AiProviderResolver`, a **sealed concrete class** (not an interface), exposing
  `AiProvider Resolve(AiProvider? requested)`, called by all three controllers — #2, #8
- All three request DTOs converge on `AiProvider?`: `CreateSessionRequest.Provider` goes nullable,
  `StartSystemLabSessionRequest` drops its `= AiProvider.Anthropic` initializer, and
  `PromptLabController.cs:62`'s `?? AiProvider.Anthropic` goes — #3
- Undefined value → `UnknownProviderException` → one row in the middleware table → 400. Delete
  `SessionController`'s two `Enum.IsDefined` provider guards — #4
- All three responses echo the resolved provider; `SystemLabSessionResponse` gains `Provider` plus
  its `FromSession` mapping — #5
- Drop the `AiProvider provider = AiProvider.Anthropic` default parameter from
  `IPromptLabService.StartChallengeAsync`; leave the model and DTO initializers alone — #6
- The CLI does not change; it keeps omitting `provider` and consciously flips Anthropic → xAI — #7
- `GetProviders()` keeps its shape; document the omit → `activeProvider` rule — #9
- Placement: resolver in `CodeSmith.Infrastructure/Configuration/` registered beside
  `services.Configure<AiOptions>(...)`; exception in `CodeSmith.Core/Exceptions/`; tests in
  `CodeSmith.Tests/Infrastructure/`
- The resolver call sits with the other validations, **before** `new NdjsonStreamWriter(...)`, so a
  throw is still a real 400
- Docs radius: `context.md`, `README.md:217`, `CLAUDE.md`'s CreateSession contract, and
  `AiOptions.cs:11`'s comment — #11

## Acceptance criteria

- `AiProviderResolverTests` cover: omitted → `ActiveProvider`; an explicit value honored **even when
  it differs from** `ActiveProvider`; an undefined value throws `UnknownProviderException`.
- Four omission tests, one per creation endpoint — `CreateSession`, `CreateSessionStream`,
  `StartChallenge`, `StartSession` — each configuring `ActiveProvider = Xai`, sending no `provider`,
  and asserting the service received `Xai`. These fail red against the current code before the fix.
- `SessionControllerTests.cs:89` becomes `Assert.ThrowsAsync<UnknownProviderException>`.
- `ExceptionHandlingMiddlewareTests` covers `UnknownProviderException → 400`.
- A numeric-garbage provider (`"provider": 999`) returns 400 on all three surfaces, not 500.
- A malformed `ActiveProvider` in config fails at host start, not at request time.
- No `IAiProviderResolver` exists anywhere; controller tests construct the real resolver from real
  options.
- `dotnet test` passes.
