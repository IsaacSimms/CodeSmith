# CodeSmith

AI-powered interview practice tool with three independent surfaces: **Tutoring** (a coding problem with starter code in a split-screen editor and a Socratic AI pair programmer that always has the current editor contents), **Prompt Lab** (prompt-engineering challenges scored against a rubric), and **System Lab** (system-design justification scenarios). All three run over one provider-agnostic LLM layer (blocking + NDJSON streaming), and every LLM call is metered against a per-user free quota + paid credit balance so the SaaS cannot run at a loss.

> **`context.md` (repo root) is the exhaustive architecture reference** — seams, full API surface, streaming contract, usage/credits, Dynamic Sessions, deploy topology, and the project's Ubiquitous Language. Consult it for anything this file doesn't cover.

## Stack

| Layer         | Technology                     |
|---------------|--------------------------------|
| Backend       | .NET 8, ASP.NET Core Web API   |
| AI            | Anthropic, OpenAI, and xAI/Grok SDKs (provider chosen per session; xAI default) |
| Payments      | Stripe.net (prepaid credit top-ups) |
| Auth          | Entra External ID (CIAM) + MSAL SPA; Development `X-Debug-User-Id` allow-list |
| Code sandbox  | Piston (local Docker default), LocalProcess (dev host), Executor (scale-to-zero Azure Container App), DynamicSessions (retained) |
| Telemetry     | OpenTelemetry → App Insights when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set |
| Frontend      | React 19, TypeScript, Vite 6   |
| Styling       | Tailwind CSS v4                |
| Data Fetching | TanStack Query v5              |
| Routing       | React Router v6                |
| E2E Testing   | Playwright                     |
| Backend Tests | xUnit, NSubstitute             |
| Frontend Tests| Vitest, React Testing Library  |

## Folder Structure

- `CodeSmith.Core/` — Domain models, enums, interfaces
- `CodeSmith.Infrastructure/` — LLM provider adapters, usage/credits, Stripe billing (`Billing/`), code execution (`Piston/`, `Executor/`, `DynamicSessions/`, LocalProcess), in-memory session stores, EF persistence
- `CodeSmith.Api/` — ASP.NET Core Web API (HTTPS 7111, HTTP 5175)
  - `Authorization/` — `[MeteredAi]` + login_required 401 handler
  - `Streaming/` — NDJSON stream writer for `/stream` endpoints
- `CodeSmith.Executor/` — Multi-language Minimal API sandbox image; deployed as a scale-to-zero Container App
- `CodeSmith.CLI/` — Command-line interface (blocking JSON)
- `CodeSmith.Tests/` — Backend unit/integration tests (Api/, CLI/, Core/, Infrastructure/)
- `CodeSmith.Web/` — React frontend (Vite dev server on port 5173)
  - `src/lib/` — API client (native fetch, no axios; `streamRequest` for NDJSON)
  - `src/auth/` — MSAL (email + Google sign-in chooser)
  - `src/features/chat|prompt-lab|system-lab|billing|home/`
  - `e2e/` — Playwright end-to-end tests
- `Docs/` — Recaps, handoffs, general Azure runbooks (Entra, Dynamic Sessions)
- `.github/workflows/` — `deploy-azure.yml`, `deploy-swa.yml`, `deploy-executor.yml` (all manual)

## API Endpoints

LLM-mutating endpoints use **`[MeteredAi]`** (subclasses `[Authorize]`). In Development an allow-listed `X-Debug-User-Id` header satisfies auth. Metered auth failures return **401** ProblemDetails with `code: "login_required"`. Any metered call can return **402** when free quota and paid credits are exhausted. **429** = IP rate limit.

Sibling **`/stream`** routes speak the NDJSON chunk contract (`delta` / `reset` / `final` / `error`); the SPA consumes those. Blocking JSON remains for the CLI. Full table + DTOs in `context.md`.

The Tutoring endpoints below are the originals; **Prompt Lab** (`/api/prompt-lab/...`), **System Lab** (`/api/system-lab/...`), code-run (`/api/session/{id}/run`), providers, and billing are documented in full in `context.md`.

### POST /api/session 🔒 `[MeteredAi]`
Create a new coding problem session.
- Request: `{ "difficulty": "Easy" | "Medium" | "Hard", "language": "CSharp" | "Cpp" | "Go" | "Rust" | "Python" | "Java" | "TypeScript", "provider": "Anthropic" | "OpenAi" | "Xai" }`
- Response (201): `{ sessionId, difficulty, language, provider, problemDescription, starterCode, messages: [], createdAt }`
- Stream sibling: `POST /api/session/stream` (description deltas + final `ProblemSession`)

### POST /api/session/{sessionId}/chat 🔒 `[MeteredAi]`
Send a message in an existing session.
- Request: `{ "message": "..." (1-2000 chars), "editorContent?": "..." (optional, max 50000 chars), "guidanceMode?": "Guidance" | "CodeAnalysis" }`
- Response (200): `{ "response": "...", "contextTokensUsed", "contextWindowSize" }`
- `editorContent` passes the current code editor contents so the AI can reference the student's actual code
- Stream sibling: `POST /api/session/{sessionId}/chat/stream`
- Errors: 400, **401**, 402, 404, 429, 502

### POST /api/session/{sessionId}/run 🔐 `[Authorize]`
Execute user code in the configured sandbox (`CodeExecution:Backend`). Authenticated but **not** LLM-metered — no `[MeteredAi]`, since a run costs sandbox CPU rather than tokens. `[Authorize]` keeps anonymous callers from driving sandbox scale-out. Dynamic Sessions requires the tutoring session id as the pool identifier; the Executor backend does not.

### Billing (Stripe prepaid credits)

Separate module from usage enforcement: **billing writes credits, enforcement debits them** — billing never references `IUsageEnforcer` or any LLM service. `objectId` comes only from `ICurrentUser`. Full seam/entity detail in `context.md`.

#### POST /api/billing/checkout 🔐 `[Authorize]`
Create a Stripe Checkout session for a credit pack.
- Request: `{ "priceId": "..." }` — must be an allow-listed Price ID (`StripeOptions.PriceIds`)
- Response (200): `{ "url": "..." }` (hosted Stripe checkout URL, redirect mode)
- Errors: 400 (unknown priceId), 401

#### POST /api/billing/webhook
Stripe completion webhook — **anonymous, signature-verified, raw body** (no model binding). Idempotent via a `ProcessedStripeEvent` dedup table; on `checkout.session.completed` it credits `amount_total` (USD) to `PaidCreditsBalance` and appends a `TopUp` ledger row atomically.
- Contract: **400** invalid signature · **200** processed / duplicate / ignored (e.g. non-USD) · **500** transient failure (Stripe retries)

#### GET /api/billing/balance 🔐 `[Authorize]`
Returns the caller's paid credits: `{ "paidCreditsUsd": <decimal> }`.

#### GET /api/billing/ledger?take=20 🔐 `[Authorize]`
Returns the caller's recent ledger rows (top-ups and spends). DTO omits `ProviderCostUsd` (margin) and `RowVersion`.

## Dev Commands

```bash
# Backend
cd CodeSmith.Api && dotnet run

# Frontend
cd CodeSmith.Web && npm run dev

# Local Piston sandbox
docker compose up -d piston

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
- Prefer streaming apiClient helpers for SPA chat/create; keep blocking paths for CLI and non-stream callers
