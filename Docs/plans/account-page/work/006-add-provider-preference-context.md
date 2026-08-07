---
id: 006
title: Add the provider-preference context
status: done
implements: [004]
depends_on: [005]
---

## Goal

Lift provider preference into a context mounted at `Layout` so all three surfaces send the provider
the UI displays, ending the defect where the picker shows xAI while the request carries Anthropic.

## Constraints

- New `src/contexts/ProviderPreferenceContext.tsx` following the `NavigationContext` pattern,
  exposing `{ provider, setProvider, availableProviders, isReady }` —
  [Design the provider-preference context](../tickets/004-design-provider-preference-context.md) contract
- `useProviderPreference` survives as the context's **internal** storage adapter; feature components
  import the context only, and the hook's four existing tests survive untouched — #1
- Its mount `useEffect` becomes a lazy `useState` initializer so returning users resolve on frame
  one and `hasStored` is trustworthy at first render — #2
- The context owns the `useProviders()` call and narrows the raw `string[]` through `isAiProvider` —
  #3
- `isReady = hasStoredChoice || query.isSuccess`, exposed instead of raw `isLoading` — #4
- `useProviders` overrides the client default with `retry: 3` and backoff — #5
- The gate is **labeled** ("Starting up…"), never an inert disabled button — #6
- The gate is bounded at ~3s, after which Start ungates and **omits** `provider` so the server
  applies `ActiveProvider` — #7, and see
  [Make AiOptions.ActiveProvider binding](005-make-active-provider-binding.md)
- The SPA's `CreateSessionRequest` type makes `provider` optional; `apiClient.test.ts` currently
  sends it on every call
- `localStorage` key `codesmith_ai_provider` is unchanged; the initializer `removeItem`s a value that
  fails `isAiProvider` — #10
- The three surface suites (`ChatWindow`, `PromptLabWindow`, `SystemLabWindow`) swap their mock
  target to the context; a new `ProviderPreferenceContext.test.tsx` carries the real assertions — #9
- Display order and labels do **not** live here; they move to the account picker in
  [Build the Preferences and Account sections](014-build-preferences-and-account-sections.md) — #8

## Acceptance criteria

- `ChatWindow`, `PromptLabWindow`, and `SystemLabWindow` read the provider from the context; no file
  outside the context imports `useProviderPreference`.
- A test proves each of the three surfaces sends the *selected* provider — not `"Anthropic"` — in
  its mutation payload.
- `ProviderPreferenceContext.test.tsx` covers: stored choice → `isReady` true on first render
  without the query; no stored choice → `isReady` false until the query succeeds; a stored invalid
  value is removed and does not count as a choice; after ~3s of a failing query, `isReady` becomes
  true and the emitted request omits `provider`.
- The Start control renders "Starting up…" while gated, never a bare disabled button.
- `npm test` passes.
