# Recap Thread — Code Sandbox Moved to a Scale-to-Zero Container App

**Date:** 2026-07-27
**Focus:** Replacing the Dynamic Sessions session-pool plan (which cannot scale to zero) with `CodeSmith.Executor` hosted as an ordinary Azure Container App at `minReplicas: 0`, plus the isolation hardening and two latent bugs that surfaced during verification.

## Why This Changed

The prior thread stalled on cost. Azure rejects `--ready-sessions 0` on **custom** Dynamic Sessions pools, so the cheapest viable pool still bills one always-warm session continuously. CodeSmith has no revenue, so a permanent idle charge was disqualifying.

**ACA Sandboxes (preview) was investigated first and rejected.** It genuinely scales to zero, but it is the wrong shape: it exists for *stateful* agent workspaces (suspend/resume with memory snapshots, volumes), while Test Code is stateless and one-shot. Decisive blockers:

- **No .NET SDK** — Python SDK + `aca` CLI only; C# listed as "coming soon". All 11 official samples are Python/CLI. A .NET adapter would hand-roll REST against `management.azuredevcompute.io`, a preview data plane Microsoft says will change.
- The app would own sandbox lifecycle (create / suspend / delete / reconcile) against an **in-memory** session store — an API restart orphans sandboxes that keep billing.
- Preview churn: "Sandboxes created during preview might not be compatible with future releases."

**The unlock:** the original reason for rejecting a plain Container App — "ACA forbids privileged containers" — applies to **Piston** (`docker-compose.yml` sets `privileged: true`), *not* to `CodeSmith.Executor`, which only calls `Process.Start`. That constraint never transferred.

## Key Outcomes

- **New `Executor` backend** at the existing `ICodeExecutionService` Seam. `$0` idle, per-second billing when running, no new Azure resource types, no preview APIs.
- **Shared wire contract extracted.** `Executor` and `DynamicSessions` talk to the same image over the same `POST /execute`, so `ExecutorContracts.cs` + `ExecutorLanguageMap.cs` now live once in `Services/Executor/` and both Adapters use them. Two Adapters over one contract is a real Seam, not a speculative one.
- **`DynamicSessions` retained** as a working Adapter — the upgrade path if microVM isolation is ever wanted.
- **All 7 languages verified running non-root in the container**, plus timeout, compile-error, exit-code, and unsupported-language paths. 431 backend tests green.

## Two Bugs Found During Verification

Both would have reached production. Neither was anticipated by the plan.

**1. `./main` never worked — Go, C++, and Rust were all broken.**
.NET on Unix resolves a relative `ProcessStartInfo.FileName` against the *host process's* current directory (`/app`), not `StartInfo.WorkingDirectory`. Every compiled language failed with ENOENT. Proven in-container: compile succeeded, binary was `-rwxr-xr-x executor executor`, ran fine from its own directory, failed only from `/app`. **Pre-existing** — identical failure as root; never caught because the executor was never smoke-tested end to end (the session pool was never created). Fixed in `CodeSmith.Executor/Program.cs` by anchoring `./` paths to the run directory; bare names still resolve through `PATH`.

**2. `AddStandardResilienceHandler()` was actively harmful here.**
Its defaults are a 10s per-attempt timeout, 30s total, and 3 retries. That would abort a 60-90s scale-from-zero cold start *and* retry non-idempotent user code — a 9-second program (inside the 10s `RunTimeoutMs` budget) would be executed four times. Removed from the new branch and from the existing DynamicSessions branch, with the reasoning recorded inline. The 120s client `Timeout` is now the only budget.

## Security Posture (Explicitly Accepted)

ACA is a **shared kernel**, not a microVM. It supports neither seccomp, AppArmor, nor capability dropping ([azure-container-apps#1232](https://github.com/microsoft/azure-container-apps/issues/1232)). Egress lockdown needs a workload-profiles environment + VNet + NAT/Firewall; `cae-codesmith-prod-centralus-001` is `Consumption` with `vnet: null`, so that is unavailable without recreating it. Real risks are **compute cost** and **outbound abuse**, not container escape.

Free mitigations applied:

| Control | Where |
|---|---|
| Non-root user (`executor`, uid 10001) | `CodeSmith.Executor/Dockerfile` |
| Internal-only ingress | `--ingress internal` |
| System-assigned identity, AcrPull only | `--system-assigned` |
| `--max-replicas 3` bounds worst-case spend | app config |
| `--scale-rule-http-concurrency 1` biases concurrent runs apart | app config |
| `[Authorize]` on `POST /api/session/{id}/run` | `SessionController.cs` |

**`POST /api/session/{id}/run` was fully anonymous.** No `[Authorize]`, no class-level `[Authorize]` — only the global 60/min/IP limiter. One signup yields a valid `sessionId`, after which code execution could be driven anonymously across rotating IPs. With scale-out that is a direct path to a surprise bill. Now carries plain `[Authorize]` — authenticated, still **never metered** (a run costs sandbox CPU, not tokens). `MeteredAiEndpointCoverageTests.SessionRunCode_DoesNotHaveMeteredAi` still passes.

**Do not attach `mi-codesmith-backend-prod-centralus-001` to the executor.** That identity reaches Key Vault and SQL; untrusted student code could mint tokens for it via IMDS.

## Azure Infrastructure — Verified, Not Assumed

| Check | Result |
|---|---|
| CAE workload profile | `Consumption`, `vnet: null` — egress lockdown unavailable |
| API scale config | `minReplicas: 0`, `maxReplicas: 3`, cooldown 300 — already scales to zero |
| Session pools in RG | Empty — failed create attempts left nothing billing |
| ACR storage | 1.31 GB of 10 GiB (Basic) — ample headroom |

An earlier ACR-exhaustion concern was **withdrawn**: layer sharing means a routine deploy adds only the `dotnet publish` layer, and Basic's 10 GiB is a billing threshold, not a hard cap.

**Stacked cold starts.** Because the API is *also* at `minReplicas: 0`, a genuinely cold first Test Code click pays API wake (~10-20s) *then* executor wake (60-90s) — approaching ~2 minutes. The 300s cooldown means only the first run after a long idle is affected.

## Dockerfile Hardening (Prompted by a Real Failure)

A network flake (`curl: (18)` mid-stream on the ~70 MB Go tarball) corrupted the pipe into `tar` and killed a 17-minute build. The image had every toolchain piped straight into `tar`/`sh` with no retries, all in one giant RUN layer. Now:

- `--retry 5 --retry-delay 3 --retry-all-errors` on every fetch
- Go downloads to disk and is verified with `gzip -t` before extraction
- Split into three layers (apt/Node · Go · Rust) so one flake no longer discards the others
- Build-time assertions (`go version`, `rustc --version`) fail the build on a broken toolchain

Rebuild after a code-only change dropped from ~17 minutes to seconds. Final image: **3.54 GB**.

Non-root required relocating toolchain state: `RUSTUP_HOME`/`CARGO_HOME` → `/opt/rust` (`chmod a+rX`), and `DOTNET_CLI_HOME`, `NUGET_PACKAGES`, `GOCACHE`, `GOPATH`, `NPM_CONFIG_CACHE` → `/home/executor`. The `dotnet new` warmup now runs *after* `USER executor` so the cache lands where the runtime user can read it.

## Artifacts Produced

**New**
- `CodeSmith.Infrastructure/Services/Executor/` — `ExecutorContracts.cs`, `ExecutorLanguageMap.cs`, `ExecutorCodeExecutionService.cs`
- `CodeSmith.Tests/Infrastructure/ExecutorCodeExecutionServiceTests.cs` — 7 tests, two pinning the distinction from DynamicSessions (no auth header, no identifier)
- `Docs/general/executor-container-app-setup.md` — ops runbook

**Modified**
- `CodeSmith.Executor/Program.cs` — relative-path execution fix
- `CodeSmith.Executor/Dockerfile` — non-root user, retry hardening, layer split
- `CodeSmith.Api/Controllers/SessionController.cs` — `[Authorize]` on `RunCode`
- `CodeSmith.Api/appsettings.json` — `CodeExecution:Executor` section
- `CodeSmith.Infrastructure/Configuration/CodeExecutionOptions.cs` — `ExecutorOptions`
- `CodeSmith.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs` — `Executor` branch; resilience handler removed from both HTTP branches
- `CodeSmith.Infrastructure/Services/DynamicSessions/*` — now consume the shared contract
- `CodeSmith.Tests/Infrastructure/CapturingHttpHandler.cs` — additive transport-failure constructor (avoids a third `StubHandler` copy)
- `.github/workflows/deploy-executor.yml` — `registry set --identity system` + `containerapp update` on the sha tag
- `context.md`, `CLAUDE.md`

## Azure Ops — In Progress

Three traps surfaced while walking the runbook live. All three are now fixed *in the runbook*, not just worked around.

**1. Unset `$RG` produced a misleading error.**
`az containerapp create` failed with `argument --resource-group/-g: expected one argument` — the variable was never set in that shell, so an empty string was passed. The runbook had the assignment tucked under "Prerequisites" where it read as optional. Now an explicit required first step with an echo-back check.

**2. `--system-assigned` + private ACR is a chicken-and-egg.**
Creating the app directly against `$ACR.azurecr.io/codesmith-executor:latest` fails:

```
Failed to provision revision ... UNAUTHORIZED: authentication required
```

The system-assigned principal is minted *during* create, so it cannot hold AcrPull yet — but the CLI validates the image pull in the same operation. The original runbook waved this away as "expected, step 3 re-points it," which was wrong. **Fix:** create against a public placeholder (`mcr.microsoft.com/k8se/quickstart:latest`), grant AcrPull, then swap in the real image. The placeholder never serves traffic. A `Start-Sleep 90` for RBAC propagation was also added — pulling too early reproduces an identical-looking `UNAUTHORIZED`.

**3. `--scale-rule-http-concurrency 1` is silently ignored on create.**
The created app shows the rule present but unconfigured:

```json
"rules": [ { "name": "exec-http", "http": { "metadata": { "concurrentRequests": "" } } } ]
```

An empty value falls back to the platform default of **10**, meaning up to ten concurrent code runs share one container — directly undoing the "one run per container" mitigation. Must be re-applied via `az containerapp update` after create. Worth verifying explicitly on any future rebuild; the create command exits 0 and looks successful.

### Ops status

| Step | State |
|---|---|
| 0. Shell variables | Done |
| 1. Delete partial app | Done |
| 2. Create w/ public placeholder | **Done** — principal `3d6ba0cf-…`, fqdn `ca-codesmith-exec-001.internal.icysea-31eca31b.centralus.azurecontainerapps.io`, `provisioningState: Succeeded` |
| 3. Grant AcrPull + propagation wait | Not done |
| 4. `registry set --identity system` | Not done |
| 5. Swap in real image **+ fix scale rule concurrency** | Not done |
| 6. API env vars → `Backend=Executor` | Not done |
| 7. Smoke | Not done |

Confirmed correct on the created app: `minReplicas: 0`, `maxReplicas: 3`, `cooldownPeriod: 300`, internal ingress, 1 CPU / 2Gi, `workloadProfileName: Consumption`.

## Current State

Code is **complete and verified locally**; **not yet committed or deployed**. Azure is partially provisioned (step 2 of 7).

Test Code will not work end to end until *both* land: the code changes deployed to `ca-codesmith-api-001` (the `Executor` backend exists only in the working tree), and ops steps 3-7 completed.

`deploy-executor.yml` is **not** required for this initial setup — the executor image is already in ACR. It is for future executor changes.

## Non-Negotiables Going Forward

- The executor never carries `mi-codesmith-backend-prod-centralus-001`, or any identity beyond AcrPull.
- The executor stays on **internal ingress**; reachability inside the CAE is the trust boundary.
- Never add `AddStandardResilienceHandler()` to a code-execution HttpClient — it retries non-idempotent user code.
- Code runs stay **outside** `IUsageEnforcer`: authenticated, never metered.
- Piston stays the local default; it can never run in ACA (needs `--privileged`).

## Open Items

- `CodeSmith.Api/appsettings.Development.json` contains plaintext Anthropic, OpenAI, and xAI keys plus a Stripe webhook secret, and is **tracked in git**. Should be rotated and moved to user-secrets.
- `TutoringService.RunCodeAsync` uses the caller-supplied `language` rather than `session.Language`.
- `CodeExecutionOptions` binds without `ValidateOnStart` — a blank `BaseUrl` fails lazily on first execution rather than at startup.
- `Docs/Handoffs.Agent/2026-07-07-stripe-billing-testing-handoff.md` showed as modified in a tree that started clean, with no edit from this thread. Worth a `git diff` before committing.
