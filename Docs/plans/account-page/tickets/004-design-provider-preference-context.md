---
id: 004
title: Design the provider-preference context
type: grilling
status: closed
blocked_by: []
---

## Question

Settled constraint 12 lifts provider preference into a context fetched once at `Layout`, fixing
a live defect where the UI can display xAI while the request sends Anthropic.

Resolve:

- What does the context expose — `{ provider, setProvider, availableProviders, isLoading }`, or
  narrower? Callers currently need only `provider`; the account page also needs `setProvider` and
  the available list.
- Does `useProviderPreference` survive as the storage adapter behind the context, or does the
  context absorb it? The hook's `localStorage` read/write and its "never persist the server
  default" rule are the parts worth keeping.
- Where does the providers query live — inside the context, or passed in from `Layout`?
- What do the three read-only consumers (`ChatWindow.tsx:39`, `PromptLabWindow.tsx:35`,
  `SystemLabWindow.tsx:29`) render while the providers query is in flight? Today they resolve
  instantly to a hardcoded default; a context that loads introduces a state they have never had.
- What happens when the user is **unauthenticated** — the providers endpoint may not be callable,
  and `Layout` renders for anonymous visitors.
- Test migration: four `vi.mock("../../../hooks/useProviderPreference")` call sites exist
  (`ChatWindow.test.tsx:13`, `PromptLabWindow.test.tsx:13`, `SystemLabWindow.test.tsx:13`,
  `HomePage.test.tsx:18`). Do they mock the context instead, or wrap in a real provider?
- Does the stored `localStorage` key change, and if so what happens to users holding the old one?

## Answer

### Contract

New `src/contexts/ProviderPreferenceContext.tsx`, mounted at `Layout`, following the
`NavigationContext` pattern:

```ts
interface ProviderPreferenceValue {
  provider: AiProvider;
  setProvider: (p: AiProvider) => void;
  availableProviders: AiProvider[];  // narrowed from the raw string[]
  isReady: boolean;                  // safe to send a provider-bearing request
}
```

`useProviderPreference` survives unchanged in role as the **internal storage adapter** behind it.
The context is its only caller; feature components import the context only.

### Decisions

| # | Decision | Reasoning |
|---|----------|-----------|
| 1 | `useProviderPreference` survives as an internal storage adapter rather than being absorbed | The `localStorage` read/write and the never-persist-the-server-default rule are a separate concern from fetch-and-broadcast. This is the internal-seam case (UL): private to the context's implementation, exercised by its own tests. `useProviderPreference.test.tsx`'s four tests survive untouched, along with their `localStorage` stub. Absorbing the hook would relocate that complexity into a component whose tests would then need the same stub — the deletion test in reverse |
| 2 | The mount `useEffect` at `useProviderPreference.ts:24-31` becomes a lazy `useState` initializer | Returning users resolve correctly on frame one instead of after a render cycle. Free correctness, and it makes `hasStored` trustworthy at first render — which decision 4 depends on |
| 3 | The context owns the `useProviders()` call and narrows the raw `string[]` through `isAiProvider` | Consumers get a typed list instead of repeating the `as AiProvider[]` cast at `HomePage.tsx:21`. Rejected passing the query down from `Layout` as props: it makes `Layout` know about provider config, and every test rendering `Layout` inherits the prop. Rejected a narrow context with the account page fetching the list separately — that is the two-independent-sources pattern that caused this defect |
| 4 | `isReady = hasStoredChoice \|\| query.isSuccess`, exposed instead of raw `isLoading` | The providers query is load-bearing **only** for users who have never chosen; for everyone else it is decoration. `retry: 1` (`App.tsx:14`) plus a scale-to-zero backend makes a failed `/api/providers` ordinary, and raw `isLoading` would let the error path ungate Start and send hardcoded `"Anthropic"` — reaching the original bug through the error door. Naming it `isReady` puts the rule at the seam instead of making three surfaces re-derive it |
| 5 | `useProviders` overrides the client default with `retry: 3` and backoff | A cold Container App 502 is expected (the HomePage footer warns users about it), and the response is `staleTime: Infinity` — worth retrying hard once, never again |
| 6 | The gate is **labeled** ("Starting up…"), never an inert disabled button | An unexplained disabled control reads as broken; a labeled one reads as loading. Matches the warm-up language already in the HomePage footer |
| 7 | The gate is **bounded at ~3s**, after which Start ungates and omits `provider` entirely | A first-time user on a dead endpoint must not be permanently stuck. Omitting is safe as a *bounded fallback* even though it was rejected as the *primary* path — as the primary path it would make the wire contract permanently ambiguous about client intent, which is the ambiguity that caused this bug; as a fallback reached only after waiting, the failure mode becomes "we asked the server to decide" rather than "we guessed Anthropic". **Depends on [010](010-resolve-request-provider-from-active-provider.md)** — see Consequences |
| 8 | Display order and labels move from `HomePage.tsx:7-14` into the account page's picker component, not the context | Presentation with exactly one renderer. By constraint 7's own logic, one consumer is a hypothetical seam |
| 9 | The three surface test suites swap the mock target to the context; a new `ProviderPreferenceContext.test.tsx` carries the real assertions | Provider resolution is incidental to what those suites test, and wrapping them in a real provider makes them depend on fetch timing (`vi.mock("../../../lib/apiClient")` auto-mocks `getProviders` to `undefined`, which TanStack rejects — every file would need an explicit `mockResolvedValue` plus `await waitFor` before any Start click). Mocking at the seam there, and pinning the fix in one dedicated file, keeps failures attributable |
| 10 | `localStorage` key `codesmith_ai_provider` is **unchanged**; the initializer `removeItem`s a value that fails `isAiProvider` | A rename would silently reset preferences for exactly the users who bothered to set one. The self-heal matters more now than before, because decision 4 makes `hasStored` decide whether Start is gated — a stored-but-invalid value must not read as "has chosen" |

### Bullets closed by codebase fact rather than by decision

- **Unauthenticated behavior** — no problem exists. `/api/providers` is anonymous:
  `SessionController.cs:18-19` carries only `[ApiController]`/`[Route("api")]`, and `GetProviders()`
  at `:37` has no `[Authorize]` or `[MeteredAi]`. It is callable by anonymous visitors at `Layout`.
- **Where the providers query lives** — inside the context (decision 3). It was already effectively
  shared via `staleTime: Infinity` (`useProviders.ts:10`); the defect was never redundant fetching,
  it was that three consumers never called it at all and so never fed `serverDefault` in.
- **What read-only consumers render while loading** — nothing. They read `provider` only inside
  mutation payloads (`ChatWindow.tsx:73`, `PromptLabWindow.tsx:95`), never in render. The gate
  belongs on the Start control, not on displayed state.
- **The fourth test call site** — `HomePage.test.tsx:18` is a **deletion, not a migration**.
  Constraint 5 removes the picker from `HomePage.tsx`, taking the mock and the display-order
  assertion at `:83-86` with it. Three files migrate, not four.

### Consequences for the map

- **Opens [Resolve the request provider from AiOptions.ActiveProvider](010-resolve-request-provider-from-active-provider.md).**
  Decision 7's fallback assumes an omitted `provider` resolves to `AiOptions.ActiveProvider`. It does
  not — every server path hardcodes Anthropic, and `ActiveProvider` is read only to *report* the
  default to clients. Decision 7 is unimplementable until 010 lands.
- **`availableProviders` is a misnomer server-side.** `SessionController.cs:39` returns
  `Enum.GetNames<AiProvider>()` unconditionally, and `AiOptions` has no notion of "configured"; all
  three providers are registered unconditionally at `ServiceCollectionExtensions.cs:103`. The
  `available.has(p)` filter at `HomePage.tsx:23` is therefore a no-op guard. Confirmed cosmetic —
  all three API keys are populated in prod, so the picker never offers a choice that fails at
  request time. Not ticketed.
- **A shared `renderWithProviders` test helper is the right long-term shape.** The three surface
  suites already hand-roll `QueryClientProvider` + `MemoryRouter` + `NavigationProvider`. Deferred
  deliberately: building it here expands the blast radius from one line per file to test
  infrastructure for three suites.
