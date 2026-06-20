# CodeSmith

An AI-powered learning platform for software engineers. Three distinct practice modes let you sharpen coding skills, prompt engineering fundamentals, and infrastructure architecture reasoning — all guided by an AI pair programmer. 

**SaaS cost controls**: Every LLM call is metered against a per-`objectId` free quota (20k tokens, 48h window) + per-IP caps + prepaid credits. Free-tier evaluations are automatically downgraded to the fast model.
An AI-powered learning platform for technologists. Three distinct practice modes let you sharpen coding skills, prompt engineering fundamentals, and infrastructure architecture reasoning — all guided by an AI pair programmer powered by your choice of xAI, Anthropic, or OpenAI models.

---

## Why It Exists

Practicing for technical interviews and building deeper software intuition requires realistic feedback loops — not multiple-choice quizzes or static tutorials. CodeSmith creates a closed feedback loop for three skill domains:

- **Coding** — Work a real problem in a real editor; the AI sees your code and guides you without giving away the answer.
- **Prompt Engineering** — Write system prompt additions to defeat a hidden adversarial instruction; get scored on rubric criteria.
- **Infrastructure Architecture** — Read a cloud scenario, write a justified design recommendation; get evaluated on architectural tradeoffs and rubric dimensions.

---

## Features

### Coding Interview Practice

Pick a language and difficulty, receive a generated coding problem with starter code in a split-screen Monaco editor, and chat with an AI pair programmer. The AI always has the current editor contents in context. A **Test Code** button submits the code to a sandboxed Piston executor and the AI can interpret the run output.

Supported languages: `CSharp`, `Cpp`, `Go`, `Rust`, `Python`, `Java`, `TypeScript`  
Difficulty levels: `Easy`, `Medium`, `Hard`

### Prompt Lab

A prompt engineering practice mode. Each challenge presents a locked base system prompt and a hidden adversarial instruction that biases the model toward bad outputs. The goal is to write prompt additions robust enough to override the bias across a battery of test inputs.

Workflow: browse challenges by category → write additions in the Monaco prompt editors → submit → review per-test pass/fail results with per-criterion rubric scores and AI evaluator feedback → iterate.

Challenge categories: Output Format Control, Specificity of Scope, Negative Instructions, Conditional Behavior, Quantity/Enumeration, Tone & Register.

**Scoring:** Each submission triggers two parallel AI phases. Phase 1 runs the assembled prompt (`locked base + hidden adversarial suffix + user additions`) against every test input using Claude Haiku. Phase 2 sends each output to Claude Sonnet acting as an expert evaluator, which scores against the rubric and returns structured feedback.

### System Lab

An infrastructure architecture practice mode. Each scenario describes a real-world cloud problem — constraints, requirements, and a set of required tradeoffs to reason through. The user writes a free-prose justification defending their design choices, then submits for AI evaluation.

Workflow: browse scenarios by category → start a session → write a justification document → submit → receive a rubric score, per-criterion breakdown, cross-cutting dimension deductions, and tradeoff analysis → iterate with guidance chat.

Scenario categories: Identity & Governance, Compute, Storage, Networking & Connectivity, Resilience & Continuity, Monitoring & Observability, Automation & IaC.

**Scoring:** Claude Sonnet evaluates the justification against the rubric criteria and a set of cross-cutting architectural dimensions (never exposed to the user). The total score is `rubric score − dimension deductions`. A guidance chat endpoint lets the user ask questions without getting the answer handed to them.

---

## Architecture

### Stack

| Layer | Technology |
|-------|-----------|
| Backend | .NET 8, ASP.NET Core Web API |
| AI | Anthropic Claude API |
| Frontend | React 19, TypeScript, Vite 6 |
| Styling | Tailwind CSS v4 |
| Data Fetching | TanStack Query v5 |
| Routing | React Router v6 |
| Code Sandbox | Piston (Docker), LocalProcess (dev fallback) |
| E2E Tests | Playwright |
| Backend Tests | xUnit, NSubstitute |
| Frontend Tests | Vitest, React Testing Library |

### Solution Structure

| Project | Role |
|---------|------|
| `CodeSmith.Core` | Domain models, enums, interfaces, exceptions — zero external dependencies |
| `CodeSmith.Infrastructure` | Service implementations, Anthropic/OpenAI/xAI integration, Piston, EF + usage repos (`CreditBalance`, `IpFreeUsage`, ledger), DI, usage enforcement |
| `CodeSmith.Api` | ASP.NET Core Web API — controllers, DTOs, middleware (incl. forwarded headers), rate limiting, CORS, debug auth |
| `CodeSmith.CLI` | Interactive console client for local testing |
| `CodeSmith.Web` | React 19 frontend — feature-based folder structure, Monaco editors, TanStack Query mutations |
| `CodeSmith.Tests` | xUnit + NSubstitute test suite mirroring the source project structure (incl. usage tests) |

### Key Seams

| Seam | Interface | Adapters |
|------|-----------|---------|
| LLM completion (unified) | `ILlmService` | `AnthropicLlmService`, `OpenAiCompatibleLlmService` (OpenAI + xAI), each wrapped by `UsageEnforcingLlmService` |
| Provider routing | `ILlmServiceFactory` | `LlmServiceFactory` (keyed by `AiProvider` at runtime) |
| Usage enforcement | `IUsageEnforcer` | `UsageEnforcer` (free quota window + paid credits) |
| Per-user usage lock | `IUserUsageLock` | `UserUsageLock` (singleton) |
| Tutoring logic | `ITutoringService` | `TutoringService` |
| Session persistence | `ISessionStore<T>` | `InMemorySessionStore`, `InMemoryPromptLabSessionStore`, `InMemorySystemLabSessionStore` |
| Code execution | `ICodeExecutionService` | `PistonCodeExecutionService` (default), `LocalProcessCodeExecutionService` (dev) |

Code execution backend is config-driven — set `CodeExecution:Backend` in `appsettings.json` to `Piston` or `LocalProcess`.

### Usage Enforcement & Cost Protection

Every LLM call is metered and protected before execution:

- **Free quota**: 20,000 tokens per `objectId`, available only for the first 48 hours after first sighting (global per objectId). Free quota is lost forever after the window or after exhaustion.
- **IP caps**: Additional 60,000-token aggregate limit per client IP (across all objectIds from that IP).
- **Paid credits**: After free is exhausted, `PaidCreditsBalance` is debited (USD-equivalent).
- Free-first deduction. Upper-bound pre-check (using highest-rate estimate) + actual record after the call.
- "Lenient last action" gate: a call that would exhaust the remaining free quota is allowed to complete; subsequent calls are blocked.
- During the free window, expensive evaluation phases (Prompt Lab / System Lab) are automatically downgraded to the Fast model to control spend. Problem generation stays on Accurate.

Enforcement lives in `UsageEnforcingLlmService` (decorator) + `UsageEnforcer` (per-objectId + per-IP logic under `IUserUsageLock`). See `CONTEXT.md` and the usage recaps for full details.

**HTTP result**: 402 Payment Required when quota/credits are insufficient.

### LLM Model Selection

| Operation | Tier (Free Window) | Tier (Paid / Window Expired) | Notes |
|-----------|--------------------|------------------------------|-------|
| Problem generation | Accurate (Sonnet) | Accurate (Sonnet) | Quality always matters |
| Chat / guidance | Fast (Haiku) | Fast (Haiku) | Latency |
| Prompt Lab simulation | Fast (Haiku) | Fast (Haiku) | Parallel, speed |
| Prompt Lab evaluation | Fast (Haiku) | Accurate (Sonnet) | Downgraded for free users to control cost |
| System Lab evaluation | Fast (Haiku) | Accurate (Sonnet) | Downgraded for free users |
| System Lab guidance chat | Fast (Haiku) | Fast (Haiku) | Latency |

Free quota uses a 48-hour window + per-objectId and per-IP caps (see Usage Enforcement below).

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/providers` | Active and available AI providers |
| `POST` | `/api/session` | Create a coding interview session |
| `POST` | `/api/session/{id}/chat` | Chat within a coding session |
| `POST` | `/api/session/{id}/run` | Execute code in the Piston sandbox |
| `GET` | `/api/prompt-lab/challenges` | List all Prompt Lab challenges |
| `GET` | `/api/prompt-lab/challenges/{id}` | Get a single challenge |
| `POST` | `/api/prompt-lab/sessions` 🔒 | Start a Prompt Lab challenge session |
| `POST` | `/api/prompt-lab/sessions/{id}/submit` 🔒 | Submit a prompt attempt for scoring |
| `GET` | `/api/system-lab/scenarios` | List all System Lab scenarios |
| `GET` | `/api/system-lab/scenarios/{id}` | Get a single scenario |
| `POST` | `/api/system-lab/sessions` 🔒 | Start a System Lab scenario session |
| `POST` | `/api/system-lab/sessions/{id}/submit` 🔒 | Submit a justification for scoring |
| `POST` | `/api/system-lab/sessions/{id}/chat` 🔒 | Guidance chat within a System Lab session |

**Note:** LLM-mutating endpoints (`🔒`) are protected by usage quota. Exhausting free quota (or IP cap) returns 402 Payment Required.

### Middleware Pipeline

Requests pass through (in order):

1. `UseExceptionHandler()` + `AppExceptionHandler` (maps domain exceptions via `IExceptionMapper` adapters)
2. `UseRequestLogging()`
3. Swagger (dev only)
4. `UseForwardedHeaders()` (for correct client IP behind proxies)
5. HTTPS Redirection
6. `UseRateLimiter()` — 60 requests / minute per client IP (fixed window, 429 on excess)
7. CORS
8. Authentication / Authorization
9. Controllers

**Quota enforcement** (402) happens inside the `UsageEnforcingLlmService` decorator before expensive LLM calls. 402 = out of quota/credits; 429 = rate limit.

### Security & Cost Protection

- API keys are never committed. `appsettings.Development.json` is gitignored.
- Error responses never include stack traces.
- Request logging never captures request or response bodies.
- User code runs inside Piston — isolated Linux container, no network access, chroot filesystem, cgroup CPU/memory/time limits. The API host process is never exposed to submitted code.
- `CodeExecution:Backend=LocalProcess` must never be used in any deployed environment.
- LLM spend is protected by `IUsageEnforcer` (pre-check + post-record) using `ICurrentUser.ObjectId` (Entra or explicit debug list). See quota details above.

---

## Development How-To

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (WSL2 is auto-configured on Windows)
- An [Anthropic API key](https://console.anthropic.com/)

---

### One-Time Setup

**1. Build the solution**

```powershell
dotnet build CodeSmith.slnx
```

**2. Configure keys and (for full features) the database**

Create `CodeSmith.Api/appsettings.Development.json` (gitignored). Minimal example:

```json
{
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "Anthropic": { "ApiKey": "sk-ant-your-key-here" },
  "Usage": {
    "AllowedDebugObjectIds": ["my-test-user-123"]
  }
}
```

For complete testing of quota, credits, and usage enforcement, also add a connection string:

```json
"ConnectionStrings": {
  "CodeSmithDb": "Server=(localdb)\\MSSQLLocalDB;Database=CodeSmithDev;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

Or use environment variables / user secrets.

**Debug users**: Set `X-Debug-User-Id: my-test-user-123` (only values listed in `AllowedDebugObjectIds` are honored). Different values act as different users for quota tracking.

**3. Start Piston and install language runtimes**

Start the container:

```powershell
docker compose up -d piston
```

Install the 7 language packages (one-time — persisted in the `piston-data` Docker volume). Rust and Java are the slowest; expect a few minutes total:

```powershell
$available = Invoke-RestMethod http://localhost:2000/api/v2/packages
foreach ($lang in @('python','typescript','go','rust','java','c++','mono')) {
    $pkg = $available | Where-Object { $_.language -eq $lang } | Select-Object -First 1
    if (-not $pkg) { Write-Warning "No package for $lang"; continue }
    Write-Host "Installing $($pkg.language) $($pkg.language_version)..."
    Invoke-RestMethod -Method Post -Uri http://localhost:2000/api/v2/packages `
        -ContentType 'application/json' `
        -Body (@{ language = $pkg.language; version = $pkg.language_version } | ConvertTo-Json)
}
```

Verify all 7 runtimes are present:

```powershell
Invoke-RestMethod http://localhost:2000/api/v2/runtimes | Select-Object language, version
```

> Piston's `ppman` CLI only exists when running from a cloned repo — not in the `ghcr.io/engineer-man/piston` image. Use the HTTP API above to manage packages.

---

### Day-to-Day: Running Locally

Three things need to be up: **Piston**, **the API**, and **the Web frontend**.

**Piston** (`restart: unless-stopped` in compose + Docker Desktop on login = usually already running):

```powershell
docker compose up -d piston
```

**API — Terminal 1:**

```powershell
dotnet run --project CodeSmith.Api --launch-profile https
```

Serves at `https://localhost:7111` (HTTPS) and `http://localhost:5175` (HTTP).  
Swagger UI available at `https://localhost:7111/swagger` in Development.

**Web frontend — Terminal 2:**

```powershell
cd CodeSmith.Web ; npm run dev
```

Frontend runs at `https://localhost:5173`. Proxies `/api/*` to the backend. Accept the self-signed cert warning on first visit.

To test as different users or exercise quota, use a browser extension to inject `X-Debug-User-Id` (value must be listed under `Usage:AllowedDebugObjectIds` in your dev settings).

**CLI (optional):**

```powershell
dotnet run --project CodeSmith.CLI
```

---

### Tests

| Scope | Command |
|-------|---------|
| All backend tests | `dotnet test CodeSmith.slnx` |
| Backend verbose | `dotnet test CodeSmith.slnx --verbosity normal` |
| Frontend unit tests | `cd CodeSmith.Web ; npm test` |
| Frontend watch mode | `cd CodeSmith.Web ; npm run test:watch` |
| Playwright E2E | `cd CodeSmith.Web ; npx playwright test` |

Playwright requires both the API and frontend running.

### Additional Documentation

- `CONTEXT.md` — Ground-truth architecture reference (seams, lifetimes, ubiquitous language, current state).
- `USER_TESTING.md` — Manual / user-based end-to-end testing guide for the full product.
- `Recaps/` — Historical design and implementation recaps (LLM seam reshape, usage enforcement, quota overhaul, deploy seam, etc.).

---

### Piston Management

| Command | Purpose |
|---------|---------|
| `docker compose up -d piston` | Start (no-op if already running) |
| `docker compose stop piston` | Stop, preserve state |
| `docker compose down` | Stop and remove container (volume kept) |
| `docker compose down -v` | Full reset — deletes installed language packages |
| `docker compose logs -f piston` | Tail logs |
| `Invoke-RestMethod http://localhost:2000/api/v2/runtimes` | List installed runtimes |

---

### Dev Fallback: Skip Piston

To run without Docker (e.g. before initial setup), add to `CodeSmith.Api/appsettings.Development.json`:

```json
"CodeExecution": { "Backend": "LocalProcess" }
```

This executes submitted code as host subprocesses. Requires `python`, `npx`/`tsx`, `g++`, `rustc`, `javac`/`java`, `go`, and `dotnet-script` on PATH. **Never use in a deployed environment.**

---

### Deployment & Production

A minimal production deploy seam now exists:

- `CodeSmith.Api/Dockerfile` (multi-stage .NET 8)
- `.dockerignore` (handles slnx + excluded projects)
- `.github/workflows/deploy-azure.yml` (manual `workflow_dispatch` to ACR + Container Apps)

**To run locally with full SaaS features** (quota, credits, usage ledger): provide a `ConnectionStrings:CodeSmithDb` pointing to SQL Server. Usage tables (`CreditBalances`, `IpFreeUsages`, `UsageLedgerEntries`) are created via EF migrations (apply separately; no auto-migrate in the app).

See `Recaps/2026-06-19-azure-deploy-seam.md` and `USER_TESTING.md` for more.

The old "before production" list in previous versions of this document has been partially implemented. Consult the Azure deploy recap and handoff docs for the current state of the deploy seam.
