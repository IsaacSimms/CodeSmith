# CodeSmith

An AI-powered coding tutor that generates C# programming problems and provides guided assistance through the Anthropic Claude API.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/) (for the React frontend)
- An [Anthropic API key](https://console.anthropic.com/)

## Solution Structure

| Project | Description |
|---------|-------------|
| `CodeSmith.Core` | Shared models, enums, interfaces, and custom exceptions |
| `CodeSmith.Infrastructure` | Anthropic SDK integration and in-memory session storage |
| `CodeSmith.Api` | ASP.NET Core Web API with rate limiting, CORS, and middleware |
| `CodeSmith.CLI` | Interactive console client for the API |
| `CodeSmith.Web` | React 19 frontend (Vite, TypeScript, Tailwind CSS, TanStack Query) |
| `CodeSmith.Tests` | xUnit backend test suite |

## Setup

### 1. Clone and build

```bash
git clone <repo-url>
cd CodeSmith
dotnet build CodeSmith.slnx
```

### 2. Configure the API key

Create or edit `CodeSmith.Api/appsettings.Development.json` (this file is gitignored):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Anthropic": {
    "ApiKey": "sk-ant-your-key-here"
  }
}
```

Alternatively, set the key via environment variable:

```bash
export Anthropic__ApiKey="sk-ant-your-key-here"
```

### 3. Run the API

The CLI expects the API on HTTPS (`https://localhost:7111`). Launch with the `https` profile:

```bash
dotnet run --project CodeSmith.Api --launch-profile https
```

The API starts on:
- HTTPS: `https://localhost:7111`
- HTTP: `http://localhost:5175`

Swagger UI is available in Development mode at `https://localhost:7111/swagger`.

### 4. Run the Web Frontend

In a separate terminal, install dependencies and start the Vite dev server:

```bash
cd CodeSmith.Web
npm install
npm run dev
```

The frontend starts on `https://localhost:5173` and automatically proxies `/api/*` requests to the backend at `https://localhost:7111`.

Open `https://localhost:5173` in your browser, select a difficulty, and start chatting with the tutor.

### 5. Run the CLI

In a separate terminal (while the API is running):

```bash
dotnet run --project CodeSmith.CLI
```

Follow the prompts to select a difficulty, view the problem, and chat with the tutor. Type `exit` or press `Ctrl+C` to quit.

## API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/session` | Create a new problem session |
| `POST` | `/api/session/{sessionId}/chat` | Send a chat message in a session |

### Create a session

```bash
curl -X POST https://localhost:7111/api/session \
  -H "Content-Type: application/json" \
  -d '{"difficulty": "Easy"}'
```

Difficulty values: `Easy`, `Medium`, `Hard`.

### Chat within a session

```bash
curl -X POST https://localhost:7111/api/session/{sessionId}/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Can you give me a hint?"}'
```

## Running Tests

### All backend tests

```bash
dotnet test CodeSmith.slnx
```

Run with verbose output:

```bash
dotnet test CodeSmith.slnx --verbosity normal
```

### Frontend unit tests (Vitest)

```bash
cd CodeSmith.Web
npm test            # single run
npm run test:watch  # watch mode
```

### Frontend E2E tests (Playwright)

```bash
cd CodeSmith.Web
npx playwright install   # first-time browser setup
npx playwright test
```

> Playwright automatically starts the Vite dev server on `https://localhost:5173` before running tests (configured in `playwright.config.ts`). The backend API must be running separately on `https://localhost:7111` for E2E tests that hit real endpoints.

## Key Commands Reference

| Command | Purpose |
|---------|---------|
| `dotnet build CodeSmith.slnx` | Build the entire solution |
| `dotnet run --project CodeSmith.Api --launch-profile https` | Start the API server (HTTPS) |
| `dotnet run --project CodeSmith.CLI` | Start the CLI client |
| `cd CodeSmith.Web && npm run dev` | Start the Vite frontend dev server |
| `dotnet test CodeSmith.slnx` | Run all backend tests |
| `dotnet test --filter "FullyQualifiedName~Core"` | Run only Core tests |
| `cd CodeSmith.Web && npm test` | Run frontend unit tests (Vitest) |
| `cd CodeSmith.Web && npx playwright test` | Run frontend E2E tests (Playwright) |
| `dotnet clean CodeSmith.slnx` | Clean all build outputs |
| `dotnet restore CodeSmith.slnx` | Restore NuGet packages |

## Rate Limiting

The API enforces a fixed window rate limit of **60 requests per minute per IP**. Exceeding this returns HTTP `429 Too Many Requests`.

---

## Testing the Website (`CodeSmith.Web`) — Full Guide

The frontend has **three layers** of tests that together verify the website and all of its wiring from isolated components up to the live API integration.

### Layer 1 — Unit Tests (Vitest + React Testing Library)

These tests run in a `jsdom` environment with **no running servers**. External dependencies (the API client, `fetch`) are mocked so every component and module is tested in isolation.

```bash
cd CodeSmith.Web
npm test
```

#### What is covered

| Test file | What it verifies |
|-----------|------------------|
| `src/lib/apiClient.test.ts` | `createSession` and `sendMessage` call the correct endpoints (`POST /api/session`, `POST /api/session/{id}/chat`) with the right HTTP method, headers, and JSON body. Verifies `ApiClientError` is thrown with status code and error body on non-ok responses. |
| `src/features/chat/components/DifficultySelector.test.tsx` | Renders all three difficulty buttons (`Easy`, `Medium`, `Hard`), fires `onSelect` with the correct value on click, disables buttons and shows loading text when `isLoading` is true. |
| `src/features/chat/components/ChatInput.test.tsx` | Renders the input and send button, trims whitespace before calling `onSend`, clears the input after submit, prevents empty/whitespace-only sends, disables during loading, and supports Enter-key submission. |
| `src/features/chat/components/MessageBubble.test.tsx` | Renders message content, aligns User messages right and Assistant messages left, applies correct colour classes (`bg-blue-600` vs `bg-gray-700`), preserves whitespace. |
| `src/features/chat/components/ChatWindow.test.tsx` | **Integration-level component test.** Mocks `apiClient` to verify the full wiring: selecting a difficulty calls `createSession`, the problem description and starter code are rendered, typing a message and pressing Enter calls `sendMessage` with the session ID, user messages appear immediately (optimistic UI), and assistant responses are appended on success. Also verifies error display when session creation fails. |

#### How mocking works

- **`apiClient.test.ts`** — stubs the global `fetch` via `vi.stubGlobal("fetch", ...)` to verify the raw HTTP requests the client makes.
- **`ChatWindow.test.tsx`** — mocks the entire `apiClient` module via `vi.mock("../../../lib/apiClient")` so the component's hooks (`useCreateSession`, `useSendMessage`) resolve with controlled data.
- Components are wrapped in `QueryClientProvider` and `MemoryRouter` to satisfy TanStack Query and React Router context requirements (mirroring the real `App.tsx` wiring).

#### Configuration

- **Vitest config** lives in the `test` block of `vite.config.ts` (environment: `jsdom`, setup file: `src/test/setup.ts`).
- **Setup file** (`src/test/setup.ts`) imports `@testing-library/jest-dom/vitest` for DOM matchers and polyfills `scrollIntoView`.

---

### Layer 2 — End-to-End Tests (Playwright)

Playwright tests run in a **real browser** against the full Vite dev server and the live backend API, verifying the complete wiring from UI → Vite proxy → ASP.NET Core API → Anthropic.

```bash
# 1. Start the backend (in a separate terminal)
dotnet run --project CodeSmith.Api --launch-profile https

# 2. Run Playwright tests
cd CodeSmith.Web
npx playwright install   # first time only
npx playwright test
```

#### Configuration

Playwright config is in `CodeSmith.Web/playwright.config.ts`:

| Setting | Value |
|---------|-------|
| Test directory | `./e2e` |
| Base URL | `https://localhost:5173` |
| HTTPS errors | Ignored (self-signed cert from `@vitejs/plugin-basic-ssl`) |
| Web server command | `npm run dev` (auto-started before tests) |
| Retries (CI) | 2 |
| Trace | On first retry |

The `webServer` block auto-starts Vite before tests run and reuses an existing server locally. In CI, set `CI=true` to force a fresh server and limit workers to 1.

#### E2E test files

Test specs live in `CodeSmith.Web/e2e/`. These tests exercise the full user flow: selecting a difficulty, waiting for the API to return a generated problem, sending chat messages, and verifying AI responses appear in the UI.

---

### Layer 3 — Backend Tests (xUnit + NSubstitute)

The backend tests in `CodeSmith.Tests/` verify the API layer that the website calls. While not inside `CodeSmith.Web`, they are essential for confirming the endpoints the frontend depends on.

```bash
dotnet test CodeSmith.slnx
```

#### Relevant backend test files

| Test file | What it verifies |
|-----------|------------------|
| `CodeSmith.Tests/Api/SessionControllerTests.cs` | `POST /api/session` returns 201 with a valid `ProblemSession`, returns 400 for invalid difficulty. `POST /api/session/{id}/chat` returns 200 with the AI response. Uses `NSubstitute` to mock `IAnthropicService`. |
| `CodeSmith.Tests/Api/ExceptionHandlingMiddlewareTests.cs` | The global error-handling middleware returns the correct JSON error shapes (`{ error, statusCode }`) that the frontend `ApiClientError` class expects. |
| `CodeSmith.Tests/Infrastructure/AnthropicServiceTests.cs` | Verifies the Anthropic SDK integration that generates problems and guidance. |

---

### Putting it all together

To verify the website and all of its wiring end-to-end, run the following in order:

```bash
# 1. Backend tests — confirm the API contracts the frontend depends on
dotnet test CodeSmith.slnx

# 2. Frontend unit tests — confirm components, hooks, and the API client work correctly in isolation
cd CodeSmith.Web
npm test

# 3. E2E tests — confirm the full stack works in a real browser
#    (start the backend first in another terminal)
dotnet run --project CodeSmith.Api --launch-profile https
#    (then, in the CodeSmith.Web directory)
npx playwright test
```

If all three layers pass, the website and its complete wiring — from React components through TanStack Query hooks, the fetch-based API client, the Vite dev proxy, the ASP.NET Core controllers, and the Anthropic service — are verified.

## Security Notes

- Never commit API keys. `appsettings.Development.json` and `appsettings.*.json` are gitignored.
- The API does not expose stack traces in error responses.
- Request logging does not capture request or response bodies.
