# Scaffold React Feature

Scaffold a new feature module in `CodeSmith.Web/src/features/` following project conventions.

## Conventions

- Strict TypeScript (`strict: true`), no `any`, named exports
- Tailwind v4 for styling (`@import "tailwindcss"` — no config file)
- API calls via TanStack Query mutations (no raw `useEffect` + `fetch`)
- API client uses native `fetch` with relative `/api` paths from `src/lib/`
- Block titles: `// == Title Here == //`
- No `/// <summary>` on individual members — inline `//` only
- Colocated unit tests as `*.test.tsx` using Vitest + React Testing Library

## Steps

1. Ask for the feature name and a brief description if not provided.
2. Present a file plan listing every file to create/edit with one-line purpose. Wait for explicit approval before writing anything.
3. Scaffold the feature folder: `src/features/<name>/` with types, hooks, and components as needed.
4. Write tests first (`*.test.tsx`) then implement until tests pass.
5. Run `npm test -- --run` and report `[N/M passed]`. Surface first 3 failures verbatim if any fail.
