# CodeSmith — Context & Architecture Reference

AI-powered coding interview practice tool. Users select a language and difficulty, receive a coding problem with starter code in a split-screen editor, and get guided assistance through an AI pair programmer.

---

## Stack

| Layer          | Technology                                         |
|----------------|----------------------------------------------------|
| Backend        | .NET 8, ASP.NET Core Web API                       |
| AI             | Anthropic Claude API, OpenAI (stub)                |
| Frontend       | React 19, TypeScript, Vite 6                       |
| Styling        | Tailwind CSS v4                                    |
| Data Fetching  | TanStack Query v5                                  |
| Routing        | React Router v6                                    |
| E2E Tests      | Playwright                                         |
| Backend Tests  | xUnit, NSubstitute                                 |
| Frontend Tests | Vitest, React Testing Library                      |
| Code Sandbox   | Piston (production), LocalProcess (dev fallback)   |

---

## Project Structure

```
CodeSmith.Core/           — Domain models, enums, interfaces, exceptions (zero external deps)
CodeSmith.Infrastructure/ — Service implementations, external integrations, DI config
CodeSmith.Api/            — ASP.NET Core Web API (HTTPS 7111, HTTP 5175)
CodeSmith.CLI/            — Console interface for local/interactive testing
CodeSmith.Tests/          — xUnit + NSubstitute tests (mirrors src project structure)
CodeSmith.Web/            — React 19 frontend (Vite dev server, port 5173)
```

### CodeSmith.Core
- `Models/` — `ProblemSession`, `ChatMessage`, `ChatResponse`, `CodeExecutionResult`, `LlmResponse`, PromptLab models
- `Interfaces/` — `ILlmService`, `ILlmServiceFactory`, `ITutoringService`, `ISessionStore`, `ICodeExecutionService`, `IPromptLabService`, `IPromptLabSessionStore`
- `Enums/` — `Language`, `Difficulty`, `MessageRole`, `AiProvider`, `ChallengeCategory`, `PromptFieldType`
- `Exceptions/` — Domain exceptions with semantic context (`SessionNotFoundException`, `AiServiceException`, etc.)

### CodeSmith.Infrastructure
- `Services/` — `TutoringService`, `AnthropicLlmService`, `OpenAiLlmService`, `LlmServiceFactory`, `InMemorySessionStore`
- `Services/Piston/` — Piston sandbox executor; `LocalProcessCodeExecutionService` as dev fallback
- `Services/PromptLab/` — `PromptLabService`, `ChallengeCatalog` (static, in-memory)
- `Configuration/` — Options classes bound to `appsettings.json` sections
- `DependencyInjection/ServiceCollectionExtensions.cs` — Single extension method (`AddCodeSmithInfrastructure`) registers everything

### CodeSmith.Api
- `Controllers/` — `SessionController`, `PromptLabController`
- `DTOs/` — Request/response models; PromptLab DTOs strip server-only fields
- `Middleware/` — `ExceptionHandlingMiddleware`, `RequestLoggingMiddleware`
- `Program.cs` — DI registration, middleware pipeline, rate limiting, CORS

### CodeSmith.Web (`src/`)
```
components/         — Shared UI (Layout, TokenUsageBar)
contexts/           — NavigationContext (cross-feature reset registry)
features/
  chat/
    components/     — ChatWindow, ChatPanel, ChatInput, CodePanel, TerminalPanel, DifficultySelector
    hooks/          — useCreateSession, useSendMessage, useRunCode, useResizableSplit
    types.ts        — Types, display helpers, type guards
  prompt-lab/
    components/     — PromptLabWindow, ChallengeSelector, PromptEditors, ResultsPanel
    hooks/          — useStartChallenge, useSubmitAttempt
    types.ts
  home/components/  — HomePage
  shared/           — monacoTheme.ts and other cross-feature utilities
hooks/              — useProviderPreference (global)
lib/                — apiClient.ts (native fetch wrapper)
test/setup.ts       — Vitest + RTL setup; mocks Monaco Editor
App.tsx             — Router + QueryClientProvider
e2e/                — Playwright tests
```

---

## Architecture Patterns

### Layering & Seams

| Seam | Interface | Implementations |
|------|-----------|----------------|
| LLM provider | `ILlmService` | `AnthropicLlmService`, `OpenAiLlmService` |
| LLM selection | `ILlmServiceFactory` | Resolves at call time based on session's `AiProvider` |
| Tutoring logic | `ITutoringService` | `TutoringService` (owns prompts, conversation history) |
| Session persistence | `ISessionStore` | `InMemorySessionStore` (singleton, `ConcurrentDictionary`) |
| Code execution | `ICodeExecutionService` | `PistonCodeExecutionService`, `LocalProcessCodeExecutionService` |
| Code backend selection | `appsettings.json` `CodeExecution:Backend` | Config-driven conditional registration at startup |

### Service Lifetimes
- **Singleton:** `ISessionStore`, `IPromptLabSessionStore`, named HTTP clients — stateless or thread-safe shared state
- **Scoped:** `ITutoringService`, `ILlmServiceFactory`, `AnthropicLlmService`, `IPromptLabService`, `ICodeExecutionService` — new instance per HTTP request

### Middleware Pipeline (order matters)
1. `ExceptionHandlingMiddleware`
2. `RequestLoggingMiddleware`
3. HTTPS Redirection
4. Rate Limiting
5. CORS
6. Controllers

### Exception → HTTP Mapping
| Exception | Status |
|-----------|--------|
| `SessionNotFoundException` | 404 |
| `ChallengeNotFoundException` | 404 |
| `AiServiceException` | 502 Bad Gateway |
| `CodeExecutionException` | 500 |
| `OperationCanceledException` | 499 Client Closed Request |
| Unknown | 500 |

Full exception logged internally; only a safe message sent to the client.

### Rate Limiting
- Fixed-window: 60 req/min per IP, `QueueLimit: 0` (reject immediately; no queuing)
- Returns 429 on exceed

### Configuration Pattern
- `appsettings.json` (prod defaults) + `appsettings.Development.json` (dev overrides)
- Sections: `"Ai"`, `"Anthropic"`, `"OpenAi"`, `"CodeExecution"`
- Each options class has a `SectionName` constant; bound via `services.Configure<T>(config.GetSection(T.SectionName))`
- Services receive `IOptions<T>` via constructor injection

---

## API Reference

| Method | Route | Request | Response | Status |
|--------|-------|---------|----------|--------|
| GET | `/api/providers` | — | `{ activeProvider, availableProviders }` | 200 |
| POST | `/api/session` | `CreateSessionRequest` | `ProblemSession` | 201 |
| POST | `/api/session/{id}/chat` | `ChatRequest` | `ChatResponse` | 200/400/404/502 |
| POST | `/api/session/{id}/run` | `RunCodeRequest` | `RunCodeResponse` | 200 |
| GET | `/api/prompt-lab/challenges` | — | `ChallengeResponse[]` | 200 |
| GET | `/api/prompt-lab/challenges/{id}` | — | `ChallengeResponse` | 200/404 |
| POST | `/api/prompt-lab/sessions` | `StartChallengeRequest` | `PromptLabSessionResponse` | 201/404 |
| POST | `/api/prompt-lab/sessions/{id}/submit` | `SubmitAttemptRequest` | `AttemptResultResponse` | 200/404 |

### Key DTOs
- **Requests:** `{Entity}Request` — e.g., `CreateSessionRequest`, `ChatRequest`, `StartChallengeRequest`
- **Responses:** `{Entity}Response` — e.g., `ChatResponse`, `ChallengeResponse`, `AttemptResultResponse`
- PromptLab response DTOs intentionally omit server-only fields (`HiddenAdversarialPrompt`, `TestInput.ExpectedBehavior`)

---

## Key Models

| Model | Key Fields |
|-------|-----------|
| `ProblemSession` | `SessionId`, `Difficulty`, `Language`, `Provider`, `ProblemDescription`, `StarterCode`, `Messages`, `CreatedAt` |
| `ChatMessage` | `Role` (User/Assistant), `Content`, `Timestamp` |
| `ChatResponse` | `Response`, `ContextTokensUsed`, `ContextWindowSize` |
| `LlmResponse` | `Content`, `InputTokensUsed`, `ContextWindowSize` |
| `CodeExecutionResult` | `Stdout`, `Stderr`, `ExitCode`, `TimedOut` |
| `Challenge` | `ChallengeId`, `Title`, `Category`, `Difficulty`, `LockedSystemPrompt`, `HiddenAdversarialPrompt`*, `EditableFields`, `TestInputs`, `Rubric` |
| `PromptLabSession` | `SessionId`, `ChallengeId`, `Provider`, `TestInputs`, `Attempts` |
| `ChallengeAttempt` | `AttemptId`, `TotalScore`, `MaxScore`, `OverallFeedback`, `Results`, `AdversarialHint` |

\* Server-only; never included in response DTOs.

---

## LLM Model Selection

| Operation | Model | Rationale |
|-----------|-------|-----------|
| Problem generation | Sonnet (accurate) | Once per session; quality matters |
| Chat guidance | Haiku (fast) | Per-message; latency and cost |
| Prompt Lab simulation | Haiku (fast) | Parallel per test input |
| Prompt Lab evaluation | Sonnet (accurate) | Rubric scoring; accuracy matters |

Constants `AccurateModel` / `FastModel` defined per service implementation.  
Context window: 200,000 tokens for all models.

### Token Accounting
- `ChatResponse.ContextTokensUsed` — actual input tokens this turn
- `ChatResponse.ContextWindowSize` — model context limit
- Frontend renders a `TokenUsageBar`; informational only, no hard enforcement

---

## PromptLab Architecture

Three-phase workflow for challenge sessions:

1. **Session Start** — `StartChallengeAsync()` uses Sonnet to dynamically generate test inputs
2. **Simulation Phase** — Parallel Haiku calls: run user's combined prompt against each test input
3. **Evaluation Phase** — Parallel Sonnet calls: score each simulation output against rubric criteria

**Effective system prompt composition:**
```
[LockedSystemPrompt] + [HiddenAdversarialPrompt] + [UserSystemPromptEdits]
```
The adversarial prompt is invisible to the user and cannot be overridden; prevents "gaming" by hardcoding expected outputs.

**Challenge Catalog** (`ChallengeCatalog.cs`) — Static in-memory collection:
- 6 categories (`OutputFormatControl`, `SpecificityOfScope`, `NegativeInstructions`, `ConditionalBehavior`, `QuantityEnumeration`, `ToneRegister`)
- 3 difficulty levels per category
- Each challenge: locked prompt, hidden adversarial prompt, 3–5 test inputs, scoring rubric

---

## Frontend Conventions

### TanStack Query
- `QueryClient` initialized in `App.tsx` with `{ retry: 1, refetchOnWindowFocus: false }`
- All async API calls use `useMutation<Response, Error, Variables>()` — no raw `useEffect` + `fetch`
- Mutations live in feature hooks (`useCreateSession`, `useSendMessage`, `useRunCode`, etc.)

### API Client (`src/lib/apiClient.ts`)
- Exports plain functions (not class methods)
- All functions call a `request<T>(url, options)` helper
- Helper adds `Content-Type: application/json`; throws `ApiClientError` on non-ok responses
- Uses relative `/api` paths; Vite dev server proxies to `http://localhost:5175`

### Local State
- Component-level `useState` for UI ephemera (input text, loading states, modal open/close)
- Session data held in component state after mutation success
- `NavigationContext` provides cross-component reset callbacks (clear session on route change)
- No Redux; TanStack Query + local state is sufficient

### Component Hierarchy (Chat feature)
```
ChatWindow
├── DifficultySelector  — initial setup before session starts
├── ChatPanel           — problem description + message list (MessageBubble per message)
├── CodePanel           — Monaco editor + TerminalPanel
└── (divider)           — useResizableSplit (percentage-based, responsive)
```

---

## Naming Conventions

### Backend
| Thing | Convention | Example |
|-------|-----------|---------|
| Files | `PascalCase.cs` | `SessionController.cs` |
| Classes | `PascalCase` | `TutoringService` |
| Interfaces | `IPascalCase` | `ILlmService` |
| Methods | `PascalCase` + verb | `GenerateProblemAsync()` |
| Request DTOs | `{Entity}Request` | `CreateSessionRequest` |
| Response DTOs | `{Entity}Response` | `ChatResponse` |
| Enums | `PascalCase` members | `Difficulty.Easy`, `Language.CSharp` |
| Options classes | `{Section}Options` | `AnthropicOptions` |

### Frontend
| Thing | Convention | Example |
|-------|-----------|---------|
| Component files | `PascalCase.tsx` | `ChatWindow.tsx` |
| Hook files | `use{Operation}.ts` | `useCreateSession.ts` |
| Props interfaces | `{Component}Props` | `ChatWindowProps` |
| Types file | `types.ts` per feature | `features/chat/types.ts` |
| Enum-like types | string literal unions | `type Language = "CSharp" \| "Python"` |
| Type guards | `is{Type}()` | `isLanguage(value): value is Language` |

---

## Code Style

### Comment Blocks
Use `// == Title Here == //` (adapt syntax per language) at the start of logical sections:
```csharp
// == Service Registration == //
// == Middleware Pipeline == //
```
```typescript
// == Create Session Hook == //
```

### XML Summary Comments
- `/// <summary>` **only** at class or interface level — never on individual methods, properties, or fields
- For members, use a standard `//` inline comment on the same line

### Inline Comments
- Same-line for single-line context; align inline comments within a block where practical
- Place above a line (not same-line) for multi-line or complex explanations

### Formatting
- C#: 4-space indentation, `PascalCase` classes/methods, `camelCase` local variables
- TypeScript: 2-space indentation, `strict: true`, no `any`, named exports only

---

## Testing Conventions

### Backend (xUnit + NSubstitute)
- **Location:** `CodeSmith.Tests/{Layer}/{SourceClass}Tests.cs`
- **Method naming:** `{Method}_{Condition}_{Expected}` — e.g., `CreateSession_WithValidDifficultyAndLanguage_Returns201`
- **Mocking:** `Substitute.For<IInterface>()` with `.Returns()` / `.Throws()`
- **Assertions:** `Assert.IsType<T>()`, `Assert.Equal()`, `Assert.Received()`

### Frontend (Vitest + React Testing Library)
- **Location:** Colocated — `Component.tsx` + `Component.test.tsx`
- **Structure:** `describe()` blocks per component; test user workflows and behavior, not implementation details
- **Interactions:** `userEvent.setup()` → `user.click()`, `user.type()`
- **Mocking:** `vi.mock()` for modules, `vi.fn()` for callbacks
- No snapshot tests

---

## Dev Commands

```bash
# Backend
cd CodeSmith.Api && dotnet run

# Frontend
cd CodeSmith.Web && npm run dev

# Backend tests
cd CodeSmith.Tests && dotnet test

# Frontend unit tests
cd CodeSmith.Web && npm test

# E2E tests
cd CodeSmith.Web && npx playwright test
```
