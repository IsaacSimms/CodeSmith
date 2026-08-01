---
id: 010
title: Resolve the request provider from AiOptions.ActiveProvider
type: grilling
status: open
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

<!-- Empty until resolved. -->
