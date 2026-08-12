# Development Guide

Full local setup, sandbox management, and deployment mechanics for CodeSmith. The [README](../README.md) carries the short quickstart; everything that only matters once you are actually running the thing lives here.

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) — for the local Piston sandbox
- An API key for at least one provider (xAI, Anthropic, or OpenAI)
- *Optional:* SQL Server / LocalDB — required for quota, credits, and the ledger
- *Optional:* Entra External ID app registration values for real SPA sign-in (Development can use the debug header instead)

---

## One-time setup

### 1. Build

```powershell
dotnet build CodeSmith.slnx
```

### 2. Configure keys and the database

Create `CodeSmith.Api/appsettings.Development.json` (gitignored):

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

`Ai:ActiveProvider` is **binding, not advisory**: omit `provider` on any LLM-creating endpoint and the server applies this value (`Anthropic` | `OpenAi` | `Xai`). An unrecognized value fails host start rather than falling back silently.

For quota, credits, and usage enforcement, add a connection string:

```json
"ConnectionStrings": {
  "CodeSmithDb": "Server=(localdb)\\MSSQLLocalDB;Database=CodeSmithDev;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

Apply EF migrations separately — **the app does not auto-migrate**. Environment variables and user-secrets work equally well for any of the above.

**Debug users.** Send `X-Debug-User-Id: my-test-user-123` to stand in for MSAL sign-in. Only values listed under `Usage:AllowedDebugObjectIds` are honored, and distinct values behave as distinct users for quota tracking. This path is Development-only and the allow-list is empty in production.

### 3. Start Piston and install language runtimes

```powershell
docker compose up -d piston
```

Install the 7 language packages. This is one-time — they persist in the `piston-data` Docker volume. Rust and Java are the slowest; budget a few minutes:

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

Verify all 7 are present:

```powershell
Invoke-RestMethod http://localhost:2000/api/v2/runtimes | Select-Object language, version
```

> Piston's `ppman` CLI only exists when running Piston from a cloned repo — it is **not** in the `ghcr.io/engineer-man/piston` image. Use the HTTP API above to manage packages.

---

## Day-to-day

Three things need to be up: Piston, the API, and the web frontend.

**Piston** — `restart: unless-stopped` in compose, so with Docker Desktop launching on login this is usually already running:

```powershell
docker compose up -d piston
```

**API (terminal 1):**

```powershell
dotnet run --project CodeSmith.Api --launch-profile https
```

Serves `https://localhost:7111` and `http://localhost:5175`. Swagger UI at `https://localhost:7111/swagger` in Development.

**Web (terminal 2):**

```powershell
cd CodeSmith.Web ; npm run dev
```

Runs at `https://localhost:5173` and proxies `/api/*` to the backend. Accept the self-signed certificate warning on first visit.

To exercise quota as different users without MSAL, inject `X-Debug-User-Id` with a browser extension (value must appear in `Usage:AllowedDebugObjectIds`).

**CLI (optional)** — talks to the blocking JSON endpoints:

```powershell
dotnet run --project CodeSmith.CLI
```

---

## Tests

| Scope | Command |
|-------|---------|
| All backend tests | `dotnet test CodeSmith.slnx` |
| Backend, verbose | `dotnet test CodeSmith.slnx --verbosity normal` |
| Frontend unit | `cd CodeSmith.Web ; npm test` |
| Frontend watch | `cd CodeSmith.Web ; npm run test:watch` |
| Playwright E2E | `cd CodeSmith.Web ; npx playwright test` |

Playwright requires both the API and the frontend to be running.

---

## Piston management

| Command | Purpose |
|---------|---------|
| `docker compose up -d piston` | Start (no-op if already running) |
| `docker compose stop piston` | Stop, preserve state |
| `docker compose down` | Stop and remove container (volume kept) |
| `docker compose down -v` | **Full reset** — deletes installed language packages |
| `docker compose logs -f piston` | Tail logs |
| `Invoke-RestMethod http://localhost:2000/api/v2/runtimes` | List installed runtimes |

---

## Dev fallback: running without Docker

Before Piston is set up, or on a machine without Docker, add to `CodeSmith.Api/appsettings.Development.json`:

```json
"CodeExecution": { "Backend": "LocalProcess" }
```

This runs submitted code as subprocesses **on the host**, and requires `python`, `npx`/`tsx`, `g++`, `rustc`, `javac`/`java`, `go`, and `dotnet-script` on `PATH`.

> **Never use `LocalProcess` in a deployed environment.** It has no isolation whatsoever.

---

## Code execution backends

`CodeExecution:Backend` selects the adapter behind `ICodeExecutionService`:

| Value | Where it's used | Isolation |
|---|---|---|
| `Piston` | Local development (default) | Docker container, per-run jail |
| `Executor` | **Azure production** — scale-to-zero Container App running the `CodeSmith.Executor` image | Shared-kernel container, internal ingress, non-root, replica-capped |
| `LocalProcess` | Dev host only | None |
| `DynamicSessions` | Retained upgrade path | Hyper-V microVM |

Production moved from `DynamicSessions` to `Executor` for cost: Azure rejects `--ready-sessions 0` on custom session pools, so even the cheapest pool bills one always-warm session continuously for a feature used in bursts. The `DynamicSessions` adapter remains in the tree for the day true microVM isolation is required. Ops detail and the security rationale for the Container App are in [`general/executor-container-app-setup.md`](general/executor-container-app-setup.md).

---

## Deployment

All three workflows are **`workflow_dispatch` only** — nothing deploys automatically on push.

| Workflow | Target |
|----------|--------|
| `.github/workflows/deploy-azure.yml` | API image → ACR → Azure Container Apps |
| `.github/workflows/deploy-swa.yml` | SPA → Azure Static Web Apps (bakes `VITE_*` at build time) |
| `.github/workflows/deploy-executor.yml` | `CodeSmith.Executor` multi-language image → ACR |

**Telemetry.** Set `APPLICATIONINSIGHTS_CONNECTION_STRING` on the API Container App to enable OpenTelemetry → Application Insights. Absent that variable, telemetry is off — which is the local default.

**Usage database.** Provide `ConnectionStrings:CodeSmithDb` and apply EF migrations. Tables cover credit balances, per-IP free usage, the usage ledger, and Stripe event deduplication.

**Secrets.** Provider API keys, Stripe keys, webhook signing secrets, and Azure credentials belong in Key Vault, GitHub secrets, or user-secrets. Never in the repo.

### Azure runbooks

| Runbook | Covers |
|---|---|
| [`general/executor-container-app-setup.md`](general/executor-container-app-setup.md) | Executor Container App — provisioning, scaling, identity, security posture |
| [`general/dynamic-sessions-azure-setup.md`](general/dynamic-sessions-azure-setup.md) | Dynamic Sessions pool, managed identity, config (retained path) |
| [`general/entra-external-id-azure-setup.md`](general/entra-external-id-azure-setup.md) | Entra External ID / CIAM wiring, Google federation |
| [`general/custom-domain-cloudflare-swa.md`](general/custom-domain-cloudflare-swa.md) | Cloudflare → Static Web Apps custom domain cutover |
| [`general/stripe-live-cutover.md`](general/stripe-live-cutover.md) | Stripe test → live mode migration |
