# CodeSmith

An AI-powered learning platform for technologists. Three distinct practice modes let you sharpen coding skills, prompt engineering fundamentals, and infrastructure architecture reasoning — all guided by an AI pair programmer powered by your choice of **xAI (default)**, Anthropic, or OpenAI models.

**SaaS cost controls:** Every LLM call is metered against a per-`objectId` free quota (20k tokens, 48h window) + per-IP caps + prepaid Stripe credits. Free-window evaluations are automatically downgraded to the Fast model tier.

---

## Why It Exists

Practicing for technical interviews and building deeper software intuition requires realistic feedback loops — not multiple-choice quizzes or static tutorials. CodeSmith creates a closed feedback loop for three skill domains:

- **Coding** — Work a real problem in a real editor; the AI sees your code and guides you without giving away the answer.
- **Prompt Engineering** — Write system prompt additions to defeat a hidden adversarial instruction; get scored on rubric criteria.
- **Infrastructure Architecture** — Read a cloud scenario, write a justified design recommendation; get evaluated on architectural tradeoffs and rubric dimensions.

---

## Features

### Coding Interview Practice

Pick a language and difficulty, receive a generated coding problem with starter code in a split-screen Monaco editor, and chat with an AI pair programmer. Problem description and chat stream token-by-token (NDJSON). The AI always has the current editor contents in context. A **Test Code** button runs code in a sandboxed executor; the terminal can show a “Starting sandbox…” hint on cold start (Azure Dynamic Sessions).

Supported languages: `CSharp`, `Cpp`, `Go`, `Rust`, `Python`, `Java`, `TypeScript`  
Difficulty levels: `Easy`, `Medium`, `Hard`

**Local vs Azure code execution:** Local dev defaults to **Piston** (Docker). Azure production uses **custom Dynamic Sessions** (Hyper-V sandboxes + multi-language `CodeSmith.Executor` image). Config: `CodeExecution:Backend` = `Piston` | `LocalProcess` | `DynamicSessions`.

### Prompt Lab

A prompt engineering practice mode. Each challenge presents a locked base system prompt and a hidden adversarial instruction that biases the model toward bad outputs. The goal is to write prompt additions robust enough to override the bias across a battery of test inputs.

Workflow: browse challenges by category → write additions in the Monaco prompt editors → submit → review per-test pass/fail results with per-criterion rubric scores and AI evaluator feedback → iterate.

Challenge categories: Output Format Control, Specificity of Scope, Negative Instructions, Conditional Behavior, Quantity/Enumeration, Tone & Register.

**Scoring:** Each submission triggers two parallel AI phases. Phase 1 runs the assembled prompt (`locked base + hidden adversarial suffix + user additions`) against every test input on the **Fast** model tier. Phase 2 scores each output against the rubric on the **Accurate** tier (downgraded to Fast while the call is covered by free quota). Provider defaults: Anthropic Haiku/Sonnet; OpenAI mini/full; xAI maps both tiers to its configured Grok models.

### System Lab

An infrastructure architecture practice mode. Each scenario describes a real-world cloud problem — constraints, requirements, and a set of required tradeoffs to reason through. The user writes a free-prose justification defending their design choices, then submits for AI evaluation.

Workflow: browse scenarios by category → start a session → write a justification document → submit → receive a rubric score, per-criterion breakdown, cross-cutting dimension deductions, and tradeoff analysis → iterate with guidance chat.

Scenario categories: Identity & Governance, Compute, Storage, Networking & Connectivity, Resilience & Continuity, Monitoring & Observability, Automation & IaC.

**Scoring:** The Accurate model tier evaluates the justification against the rubric criteria and a set of cross-cutting architectural dimensions (never exposed to the user). The total score is `rubric score − dimension deductions`. Free-window evaluations are tier-downgraded like Prompt Lab. A guidance chat endpoint lets the user ask questions without getting the answer handed to them.

### Auth & billing (SPA)

- **Sign in:** Entra External ID (CIAM) via MSAL — **email** or **Google** federation. API uses Entra-issued bearer tokens; there is no separate Google JWT stack on the API.
- **Credits:** Stripe Checkout prepaid packs; balance and ledger in the SPA after sign-in.

---

## Architecture

### Stack

| Layer | Technology |
|-------|-----------|
| Backend | .NET 8, ASP.NET Core Web API |
| AI | Anthropic SDK; OpenAI SDK (also drives xAI/Grok via OpenAI-compatible endpoint); xAI default |
| Payments | Stripe.net (prepaid credit top-ups) |
| Auth | Entra External ID (CIAM) + MSAL on the SPA; Development debug header allow-list |
| Persistence | EF Core + SQL Server (usage/credits); in-memory session stores |
| Code sandbox | Piston (Docker, local default), LocalProcess (dev host), DynamicSessions (Azure) |
| Telemetry | OpenTelemetry → Azure Monitor / Application Insights (when connection string is set) |
| Frontend | React 19, TypeScript, Vite 6 |
| Styling | Tailwind CSS v4 |
| Data Fetching | TanStack Query v5 |
| Routing | React Router v6 |
| E2E Tests | Playwright |
| Backend Tests | xUnit, NSubstitute |
| Frontend Tests | Vitest, React Testing Library |

### Solution Structure

| Project | Role |
|---------|------|
| `CodeSmith.Core` | Domain models, enums, interfaces, exceptions — zero external dependencies |
| `CodeSmith.Infrastructure` | LLM adapters, usage/credits, Stripe billing, code execution (Piston / LocalProcess / Dynamic Sessions), EF, DI |
| `CodeSmith.Api` | ASP.NET Core Web API — controllers, DTOs, middleware, rate limiting, CORS, MeteredAi auth, NDJSON streaming |
| `CodeSmith.Executor` | Multi-language Minimal API image for Azure custom Dynamic Sessions (not used for local Piston) |
| `CodeSmith.CLI` | Interactive console client for local testing (blocking JSON endpoints) |
| `CodeSmith.Web` | React 19 SPA — feature folders, Monaco, TanStack Query, MSAL, streaming chat |
| `CodeSmith.Tests` | xUnit + NSubstitute suite mirroring source layout |

### Key Seams

| Seam | Interface | Adapters |
|------|-----------|---------|
| LLM completion (unified) | `ILlmService` (`CompleteAsync` + `StreamAsync`) | `AnthropicLlmService`, `OpenAiCompatibleLlmService` (OpenAI + xAI), each wrapped by `UsageEnforcingLlmService` |
| Provider routing | `ILlmServiceFactory` | `LlmServiceFactory` (keyed by `AiProvider` at runtime) |
| Usage enforcement | `IUsageEnforcer` | `UsageEnforcer` (free window + IP cap + paid credits; reserve → settle / release) |
| Per-user usage lock | `IUserUsageLock` | `UserUsageLock` (singleton) |
| Tutoring logic | `ITutoringService` | `TutoringService` |
| Session persistence | `ISessionStore<T>` | In-memory stores per surface |
| Code execution | `ICodeExecutionService` | `PistonCodeExecutionService`, `LocalProcessCodeExecutionService`, `DynamicSessionsCodeExecutionService` |
| Billing | `IBillingService` | `StripeBillingService` |

Code execution backend is config-driven — set `CodeExecution:Backend` in `appsettings.json` to `Piston`, `LocalProcess`, or `DynamicSessions`. Full seam detail lives in `context.md`.

### Usage Enforcement & Cost Protection

Every LLM call is metered and protected before execution:

- **Free quota**: 20,000 tokens per `objectId`, available only for the first 48 hours after first sighting. Free quota is lost after the window or after exhaustion (no monthly reset).
- **IP caps**: 60,000-token aggregate free-token limit per client IP (across all objectIds).
- **Paid credits**: After free coverage is exhausted, `PaidCreditsBalance` is debited (USD-equivalent charge = provider cost × markup).
- Free-first deduction. Upper-bound **reserve** (persisted hold) before the call; **settle** to actuals on success; **release** on failure. Insufficient budget → hard fail (402) — there is no “lenient last free call.”
- During the free window, expensive evaluation features are automatically downgraded to the Fast model. Problem generation stays on Accurate.

Enforcement lives in `UsageEnforcingLlmService` (decorator) + `UsageEnforcer`. See `context.md` and `Docs/Recaps/` for full details.

**HTTP results on metered AI routes:** **401** login required (`[MeteredAi]`); **402** insufficient free window + paid credits; **429** IP rate limit (60 req/min).

### LLM Model Selection

| Operation | Tier (Free Window) | Tier (Paid / Window Expired) | Notes |
|-----------|--------------------|------------------------------|-------|
| Problem generation | Accurate | Accurate | Quality always matters |
| Chat / guidance | Fast | Fast | Latency |
| Prompt Lab simulation | Fast | Fast | Parallel, speed |
| Prompt Lab evaluation | Fast | Accurate | Downgraded while free quota covers the call |
| System Lab evaluation | Fast | Accurate | Same free-window downgrade |
| System Lab guidance chat | Fast | Fast | Latency |

Concrete model names are per-provider config (`Anthropic` / `OpenAi` / `Xai` options), validated against the pricing catalog at startup.

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/providers` | Active and available AI providers |
| `POST` | `/api/session` 🔒 | Create a coding interview session |
| `POST` | `/api/session/stream` 🔒 | Same, NDJSON stream (description deltas + final session) |
| `POST` | `/api/session/{id}/chat` 🔒 | Chat within a coding session |
| `POST` | `/api/session/{id}/chat/stream` 🔒 | Chat NDJSON stream |
| `POST` | `/api/session/{id}/run` | Execute code in the configured sandbox (not LLM-metered) |
| `GET` | `/api/prompt-lab/challenges` | List Prompt Lab challenges |
| `GET` | `/api/prompt-lab/challenges/{id}` | Get a single challenge |
| `POST` | `/api/prompt-lab/sessions` 🔒 | Start a Prompt Lab session |
| `POST` | `/api/prompt-lab/sessions/{id}/submit` 🔒 | Submit a prompt attempt for scoring |
| `POST` | `/api/prompt-lab/sessions/{id}/chat` 🔒 | Guidance chat |
| `POST` | `/api/prompt-lab/sessions/{id}/chat/stream` 🔒 | Guidance chat stream |
| `GET` | `/api/system-lab/scenarios` | List System Lab scenarios |
| `GET` | `/api/system-lab/scenarios/{id}` | Get a single scenario |
| `POST` | `/api/system-lab/sessions` 🔒 | Start a System Lab session |
| `POST` | `/api/system-lab/sessions/{id}/submit` 🔒 | Submit a justification for scoring |
| `POST` | `/api/system-lab/sessions/{id}/chat` 🔒 | Guidance chat |
| `POST` | `/api/system-lab/sessions/{id}/chat/stream` 🔒 | Guidance chat stream |
| `POST` | `/api/billing/checkout` 🔐 | Create Stripe Checkout session (allow-listed priceId) |
| `POST` | `/api/billing/webhook` | Stripe webhook — anonymous, signature-verified, raw body |
| `GET` | `/api/billing/balance` 🔐 | Paid credit balance |
| `GET` | `/api/billing/ledger` 🔐 | Recent ledger rows |

- **🔒** = `[MeteredAi]` — auth required; failures return 401 ProblemDetails (`login_required`). Exhausted quota/credits → 402.
- **🔐** = `[Authorize]` (billing reads/checkout) — auth required; stock 401 (not the metered login_required body).
- SPA uses the `/stream` siblings; CLI still uses blocking JSON. Full contracts (including NDJSON chunk types) are in `context.md`.

### Middleware Pipeline

Requests pass through (in order):

1. `UseExceptionHandler()` + `AppExceptionHandler` (declarative domain exception → ProblemDetails table)
2. `UseRequestLogging()`
3. Swagger (dev only)
4. `UseForwardedHeaders()` (correct client IP behind proxies — load-bearing for rate limit + IP free cap)
5. HTTPS Redirection
6. `UseRateLimiter()` — 60 requests / minute per client IP (fixed window, 429 on excess)
7. CORS
8. Authentication / Authorization
9. Controllers

Quota enforcement (402) happens inside the usage decorator around LLM calls, not in middleware.

### Security & Cost Protection

- API keys and secrets are never committed. `appsettings.Development.json` is gitignored; use user-secrets / env / Key Vault in deploy.
- Error responses never include stack traces.
- Request logging never captures request or response bodies.
- User code runs in a sandbox — **Piston** (local: isolated container) or **Azure Dynamic Sessions** (Hyper-V; egress disabled in the ops runbook). `LocalProcess` must never be used in any deployed environment.
- LLM spend is protected by `IUsageEnforcer` using `ICurrentUser.ObjectId` (Entra or Development debug allow-list).

---

## Development How-To

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for local Piston)
- API keys for the provider(s) you will exercise (xAI and/or Anthropic and/or OpenAI)
- Optional: SQL Server / LocalDB for usage, credits, and ledger
- Optional: Entra External ID app registration values for real SPA sign-in (Development can use the debug header instead)

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
  "Ai": { "ActiveProvider": "Xai" },
  "Xai": { "ApiKey": "your-xai-key" },
  "Anthropic": { "ApiKey": "sk-ant-optional" },
  "OpenAi": { "ApiKey": "sk-optional" },
  "Usage": {
    "AllowedDebugObjectIds": ["my-test-user-123"]
  }
}
```

For quota, credits, and usage enforcement, also add:

```json
"ConnectionStrings": {
  "CodeSmithDb": "Server=(localdb)\\MSSQLLocalDB;Database=CodeSmithDev;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

Apply EF migrations separately (the app does not auto-migrate). Or use environment variables / user secrets.

**Debug users:** Set `X-Debug-User-Id: my-test-user-123` (only values listed in `AllowedDebugObjectIds` are honored). Different values act as different users for quota tracking. This path is Development-only.

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

To test as different users or exercise quota without MSAL, use a browser extension to inject `X-Debug-User-Id` (value must be listed under `Usage:AllowedDebugObjectIds`).

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

- `context.md` — Ground-truth architecture reference (seams, lifetimes, API contracts, ubiquitous language).
- `USER_TESTING.md` — Manual / user-based end-to-end testing guide.
- `Docs/Recaps/` — Historical design and implementation recaps.
- `Docs/general/dynamic-sessions-azure-setup.md` — Azure Dynamic Sessions pool / MI / config runbook.
- `Docs/general/entra-external-id-azure-setup.md` — Entra External ID wiring notes.

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

Manual GitHub Actions deploy seams (all `workflow_dispatch` — nothing auto-deploys on push):

| Workflow | Target |
|----------|--------|
| `.github/workflows/deploy-azure.yml` | API image → ACR → Azure Container Apps |
| `.github/workflows/deploy-swa.yml` | SPA → Azure Static Web Apps (bakes `VITE_*` at build) |
| `.github/workflows/deploy-executor.yml` | `CodeSmith.Executor` multi-lang image → ACR (Dynamic Sessions pool) |

**Code execution in Azure:** Piston needs privileged containers; ACA does not allow that. Production uses **custom Dynamic Sessions** + the executor image. One-time pool, role assignment, and `CodeExecution__Backend=DynamicSessions` + pool endpoint on the API app are documented in `Docs/general/dynamic-sessions-azure-setup.md`. Repo code is implemented; pool wiring is an ops step.

**Telemetry:** set `APPLICATIONINSIGHTS_CONNECTION_STRING` on the API Container App to enable OpenTelemetry → App Insights (local without it runs telemetry-off).

**Usage DB:** provide `ConnectionStrings:CodeSmithDb` and apply EF migrations. Tables cover credit balances, IP free usage, usage ledger, and Stripe event dedup.

Do not commit secrets. Stripe keys, provider API keys, webhook secrets, and Azure credentials live in Key Vault / GitHub secrets / user-secrets — never in this README.
