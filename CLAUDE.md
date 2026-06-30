# CodeSmith

AI-powered interview practice tool with three independent surfaces: **Tutoring** (a coding problem with starter code in a split-screen editor and a Socratic AI pair programmer that always has the current editor contents), **Prompt Lab** (prompt-engineering challenges scored against a rubric), and **System Lab** (system-design justification scenarios). All three run over one provider-agnostic LLM layer, and every LLM call is metered against a per-user free quota + paid credit balance so the SaaS cannot run at a loss.

> **`context.md` (repo root) is the exhaustive architecture reference** — seams, full API surface, the usage/credits subsystem, and the project's Ubiquitous Language. Consult it for anything this file doesn't cover.

## Stack

| Layer         | Technology                     |
|---------------|--------------------------------|
| Backend       | .NET 8, ASP.NET Core Web API   |
| AI            | Anthropic, OpenAI, and xAI/Grok SDKs (provider chosen per session; xAI default) |
| Frontend      | React 19, TypeScript, Vite 6   |
| Styling       | Tailwind CSS v4                |
| Data Fetching | TanStack Query v5              |
| Routing       | React Router v6                |
| E2E Testing   | Playwright                     |
| Backend Tests | xUnit, NSubstitute             |
| Frontend Tests| Vitest, React Testing Library  |

## Folder Structure

- `CodeSmith.Core/` — Domain models, enums, interfaces
- `CodeSmith.Infrastructure/` — LLM provider adapters, usage/credits enforcement, code execution, in-memory session stores, EF persistence
- `CodeSmith.Api/` — ASP.NET Core Web API (HTTPS 7111, HTTP 5175)
- `CodeSmith.CLI/` — Command-line interface
- `CodeSmith.Tests/` — Backend unit/integration tests (Api/, CLI/, Core/, Infrastructure/)
- `CodeSmith.Web/` — React frontend (Vite dev server on port 5173)
  - `src/lib/` — API client (native fetch, no axios)
  - `src/features/chat/` — Types, hooks, components
  - `e2e/` — Playwright end-to-end tests

## API Endpoints

LLM-mutating endpoints require auth (`[Authorize]`; in Development an allow-listed `X-Debug-User-Id` header satisfies it). Any metered call can return **402** when free quota and paid credits are exhausted. The Tutoring endpoints below are the originals; the **Prompt Lab** (`/api/prompt-lab/...`), **System Lab** (`/api/system-lab/...`), code-run (`/api/session/{id}/run`), and `/api/providers` endpoints are documented in full in `context.md`.

### POST /api/session 🔒
Create a new coding problem session.
- Request: `{ "difficulty": "Easy" | "Medium" | "Hard", "language": "CSharp" | "Cpp" | "Go" | "Rust" | "Python" | "Java" | "TypeScript", "provider": "Anthropic" | "OpenAi" | "Xai" }`
- Response (201): `{ sessionId, difficulty, language, provider, problemDescription, starterCode, messages: [], createdAt }`

### POST /api/session/{sessionId}/chat 🔒
Send a message in an existing session.
- Request: `{ "message": "..." (1-2000 chars), "editorContent?": "..." (optional, max 50000 chars), "guidanceMode?": "Guidance" | "CodeAnalysis" }`
- Response (200): `{ "response": "...", "contextTokensUsed", "contextWindowSize" }`
- `editorContent` passes the current code editor contents so the AI can reference the student's actual code
- Errors: 400, 402, 404, 429, 502

## Dev Commands

```bash
# Backend
cd CodeSmith.Api && dotnet run

# Frontend
cd CodeSmith.Web && npm run dev

# Tests
cd CodeSmith.Tests && dotnet test
cd CodeSmith.Web && npm test              # Vitest unit tests
cd CodeSmith.Web && npx playwright test   # E2E tests
```

## Testing

- Unit tests are required when adding new features
- Backend tests live in `CodeSmith.Tests/` mirroring the project they cover (e.g., `Api/` for `CodeSmith.Api/`)
- Frontend unit tests use Vitest + React Testing Library, colocated as `*.test.tsx` alongside source files
- Frontend E2E tests use Playwright in `CodeSmith.Web/e2e/`

## Browser Testing with Playwright MCP

**Only use Playwright MCP when explicitly asked to by the user.** Do not initiate browser checks automatically after frontend changes.

When requested, the workflow is:
1. Ensure dev servers are running: backend on `http://localhost:5175`, frontend on `http://localhost:5173`
2. Use `browser_navigate` to open `http://localhost:5173`
3. Use `browser_snapshot` to inspect the page structure, or `browser_screenshot` to visually verify layout
4. Interact with the app as needed (`browser_click`, `browser_type`) to exercise the changed behavior
5. Report what was observed — confirm the change works or flag anything unexpected

## Coding Conventions

- Block titles: `// == Title Here == //` (adapt syntax per language)
- `/// <summary>` only at class/interface level, never on members
- TypeScript: `strict: true`, no `any`, named exports, feature-based folders
- Tailwind v4: `@import "tailwindcss"` in CSS, no config file needed
- API client uses native `fetch` with relative `/api` paths
- All API calls use TanStack Query mutations (no raw `useEffect` + `fetch`)
