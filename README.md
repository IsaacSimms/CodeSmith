# CodeSmith

An AI-powered coding interview practice tool. Users pick a programming language and difficulty, receive a tailored coding problem with starter code in a split-screen editor, and get guided assistance from an AI pair programmer powered by the Anthropic Claude API. The AI always has access to the current contents of the code editor, so it can reference and reason about the users's actual code.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/) (for the React frontend)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for the Piston code execution sandbox; WSL2 is auto-configured on Windows)
- An [Anthropic API key](https://console.anthropic.com/)

## Solution Structure

| Project | Description |
|---------|-------------|
| `CodeSmith.Core` | Shared models, enums, interfaces, and custom exceptions |
| `CodeSmith.Infrastructure` | Anthropic SDK integration and in-memory session storage |
| `CodeSmith.Api` | ASP.NET Core Web API with rate limiting, CORS, and middleware |
| `CodeSmith.CLI` | Interactive console client for the API |
| `CodeSmith.Web` | React 19 frontend (Vite, TypeScript, Tailwind CSS v4, TanStack Query v5) |
| `CodeSmith.Tests` | xUnit + NSubstitute backend test suite |

The "Test Code" feature does not run user code on the host. Submissions are forwarded to [Piston](https://github.com/engineer-man/piston), a self-hosted sandbox that executes each run inside an isolated Linux container with no network access, a chroot filesystem, and cgroup CPU/memory/time limits. Piston itself runs as a Docker container on `localhost:2000`, defined in [`docker-compose.yml`](./docker-compose.yml) so every dev machine uses the same configuration and the same manifest carries into production.

## Setup (one-time)

Do these steps once after cloning. Once complete, everything persists — `Running Locally` below is what you touch day-to-day.

### 1. Clone and build

```bash
git clone <repo-url>
cd CodeSmith
dotnet build CodeSmith.slnx
```

### 2. Configure the Anthropic API key

Create `CodeSmith.Api/appsettings.Development.json` (gitignored):

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

Alternatively, export it as an environment variable:

```bash
export Anthropic__ApiKey="sk-ant-your-key-here"
```

### 3. Start the Piston sandbox and install language packages

Start the container (reads `docker-compose.yml` automatically):

```bash
docker compose up -d piston
```

Install the 7 language packages CodeSmith supports. This is a one-time step — the packages persist in the `piston-data` named volume across container restarts.

```bash
docker exec piston_api /piston/cli/index.js ppman install python
docker exec piston_api /piston/cli/index.js ppman install typescript
docker exec piston_api /piston/cli/index.js ppman install go
docker exec piston_api /piston/cli/index.js ppman install rust
docker exec piston_api /piston/cli/index.js ppman install java
docker exec piston_api /piston/cli/index.js ppman install c++
docker exec piston_api /piston/cli/index.js ppman install mono   # C#
```

Verify:

```bash
curl http://localhost:2000/api/v2/runtimes
```

The response should list all 7 runtimes.

## Running Locally (day-to-day)

Running or testing the app locally means having three things up at once: **Piston**, **the API**, and **the Web frontend**. Piston stays up across reboots on its own; the API and frontend are what you start each session.

### 1. Confirm Piston is running

With `restart: unless-stopped` in the compose file and Docker Desktop set to start on login, Piston is usually already up. If not:

```bash
docker compose up -d piston
```

This is a no-op if it's already running.

### 2. Start the API (Terminal 1)

The CLI and frontend both expect the API on HTTPS at `https://localhost:7111`. Launch with the `https` profile:

```bash
dotnet run --project CodeSmith.Api --launch-profile https
```

The API serves:
- HTTPS: `https://localhost:7111`
- HTTP: `http://localhost:5175`
- Swagger UI (Development only): `https://localhost:7111/swagger`

### 3. Start the Web frontend (Terminal 2)

```bash
cd CodeSmith.Web
npm install       # first run only
npm run dev
```

The frontend runs at `https://localhost:5173` and proxies `/api/*` to the backend. Accept the self-signed cert warning on first visit.

Open `https://localhost:5173`, pick a language and difficulty, and you're in.

### Optional: Run the CLI

In a separate terminal, while the API is running:

```bash
dotnet run --project CodeSmith.CLI
```

Follow the prompts; `exit` or `Ctrl+C` to quit.

### Running tests

| Scope | Command |
|-------|---------|
| All backend tests | `dotnet test CodeSmith.slnx` |
| Backend verbose | `dotnet test CodeSmith.slnx --verbosity normal` |
| Frontend unit tests | `cd CodeSmith.Web && npm test` |
| Frontend watch mode | `cd CodeSmith.Web && npm run test:watch` |
| Playwright E2E | `cd CodeSmith.Web && npx playwright test` |

Playwright E2E tests require both the API and frontend to be running.

### Piston management commands

| Command | Purpose |
|---------|---------|
| `docker compose up -d piston` | Start Piston (no-op if already running) |
| `docker compose stop piston` | Stop Piston (persisted state kept) |
| `docker compose down` | Stop and remove the container (volume is preserved) |
| `docker compose down -v` | Full reset — also deletes installed language packages |
| `docker compose logs -f piston` | Tail Piston logs |
| `curl http://localhost:2000/api/v2/runtimes` | List installed languages |

### Fallback: in-process execution (dev only, unsafe)

To bypass Piston during development (e.g. before Docker is set up), set:

```json
"CodeExecution": {
  "Backend": "LocalProcess"
}
```

in `CodeSmith.Api/appsettings.Development.json`. This runs submitted code as subprocesses on the host with the API's permissions. It requires the host to have `python`, `npx`/`tsx`, `g++`, `rustc`, `javac`/`java`, `go`, and `dotnet-script` available on PATH, and should never be used in any deployed environment.

## Before Production Deployment

Today the compose file only defines Piston — the API still runs via `dotnet run` on the host. Before deploying publicly, the API itself needs to be containerized so the whole stack ships as one unit. At that time you'll need to:

1. **Write `CodeSmith.Api/Dockerfile`** — a standard multi-stage .NET 8 build (SDK image for `dotnet publish`, runtime image for the final layer).
2. **Add an `api` service to `docker-compose.yml`** that builds from the Dockerfile, depends on the `piston` service, and talks to it over the internal Docker network (set `CodeExecution:Piston:BaseUrl=http://piston:2000` — Docker DNS resolves the service name).
3. **Add a `docker-compose.prod.yml` overlay** with production-only concerns: the Anthropic API key sourced from a secret manager (not `appsettings.Development.json`), no source bind-mounts, tighter restart policies, and log drivers. Deploy with `docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d`.
4. **Stop exposing Piston's port publicly** — remove the `ports: ["2000:2000"]` mapping for Piston in the prod overlay so only the API (on the internal network) can reach it. The public-facing surface is the API alone.
5. **Add per-user rate limiting.** The current global 60 req/min limit helps but isn't enough to stop a single abusive user from burning compute on Test Code runs.
6. **Pick a host** (Fly.io, Railway, a VPS running Docker, AWS ECS, etc.) and point it at the compose file. Most managed platforms consume `docker-compose.yml` directly or want a trivially equivalent manifest.

None of the above requires code changes — it's packaging and configuration work. The `ICodeExecutionService` seam, the Piston language map, and the API itself are already shaped correctly for it.

## API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/session` | Create a new problem session |
| `POST` | `/api/session/{sessionId}/chat` | Send a chat message in a session |
| `POST` | `/api/session/{sessionId}/run` | Execute the session's code in the Piston sandbox |

### Create a session

```bash
curl -X POST https://localhost:7111/api/session \
  -H "Content-Type: application/json" \
  -d '{"difficulty": "Easy", "language": "CSharp"}'
```

Difficulty values: `Easy`, `Medium`, `Hard`.

Language values: `CSharp`, `Cpp`, `Go`, `Rust`, `Python`, `Java`, `TypeScript`.

### Chat within a session

```bash
curl -X POST https://localhost:7111/api/session/{sessionId}/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Can you give me a hint?", "editorContent": "public int Add(int a, int b) { return 0; }"}'
```

`editorContent` is optional. When provided, the AI uses it as context to reference the student's current code in the editor. When omitted, the AI only sees the original starter code.

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
| `dotnet clean CodeSmith.slnx` | Clean all build outputs |
| `dotnet restore CodeSmith.slnx` | Restore NuGet packages |
| `docker compose up -d piston` | Start the Piston sandbox container |
| `curl http://localhost:2000/api/v2/runtimes` | Verify Piston is running and list installed languages |

## Rate Limiting

The API enforces a fixed window rate limit of **60 requests per minute per IP**. Exceeding this returns HTTP `429 Too Many Requests`.

## Security Notes

- Never commit API keys. `appsettings.Development.json` and `appsettings.*.json` are gitignored.
- The API does not expose stack traces in error responses.
- Request logging does not capture request or response bodies.
- User-submitted code runs inside a Piston container with no network access, a chroot filesystem, and cgroup CPU/memory/time limits. The API host process is not exposed to submitted code. Do not deploy with `CodeExecution:Backend=LocalProcess`.
