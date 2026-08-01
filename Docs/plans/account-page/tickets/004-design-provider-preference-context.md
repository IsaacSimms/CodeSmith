---
id: 004
title: Design the provider-preference context
type: grilling
status: open
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

<!-- Empty until resolved. -->
