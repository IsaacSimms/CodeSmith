# Azure Dynamic Sessions Code Sandbox

**Date:** 2026-07-22  
**Type:** implementation  
**Environment / Systems:** CodeSmith (.NET 8 / React), Azure Container Apps, ACR `acrcodesmithprod001`, Central US

## TL;DR

Production code execution cannot run as a privileged Piston Container App (ACA forbids privileged containers). The team grilled that design, replaced it with ACA **custom Dynamic Sessions** + a multi-lang executor image, and implemented the Core/Infrastructure/Executor/Web/CI pieces in-repo. Azure session-pool provisioning remains a one-time ops step.

## Context & Goal

Carry local Tutoring **Test Code** (Piston via Docker) into Azure with scale-to-zero / low idle cost. An ideation handoff proposed `ca-codesmith-piston-001` plus a new `IPistonClient` seam. Goal of this thread: pressure-test that design, then build the viable path.

## Key Points Explored

- **Existing seams:** `ICodeExecutionService`, `PistonCodeExecutionService`, `PistonOptions`, DI `CodeExecution:Backend`, `POST /api/session/{id}/run`, and `docker-compose` Piston already exist — no greenfield client needed.
- **Hard platform limit:** local Piston requires `privileged: true`; ACA does not allow privileged containers.
- **Azure options weighed:** built-in Dynamic Sessions (Python-centric), custom Dynamic Sessions (BYO image, Hyper-V isolation), hosted Judge0, VM + privileged Piston, or no prod execution.
- **Cost vs cold start:** language count does not drive idle $ when `readySessionInstances=0`; it mainly grows image size and first-Test-Code latency (~30–90s after idle). Sandbox wakes on **Test Code**, not on opening the app.
- **Session identity:** reuse tutoring `sessionId` as Dynamic Sessions `identifier` so later Test Codes in the same problem stay warm.
- **Egress:** disabled for the sandbox (untrusted student code).

## Decisions & Outcomes

| Decision | Outcome |
|----------|---------|
| Host | ACA **custom Dynamic Sessions**, not plain Container App Piston |
| Languages v1 | All 7 Tutoring languages; C# via **dotnet SDK** |
| Local vs Azure | Local remains **Piston**; Azure uses **DynamicSessions** |
| Seam change | `CodeExecutionRequest` DTO; `ExecuteAsync(CodeExecutionRequest, ct)` |
| Executor | New `CodeSmith.Executor` Minimal API + fat multi-lang Dockerfile |
| UX | After 5s pending: “Starting sandbox…” in terminal panel |
| Deploy | `deploy-executor.yml` for image; runbook for one-time pool/role/config |
| Quotas | Code runs still outside `IUsageEnforcer` |

**Shipped in repo:** request DTO + adapter updates, `DynamicSessionsCodeExecutionService` + `Azure.Identity` token provider, DI branch, executor project, frontend hint, GH workflow, `Docs/general/dynamic-sessions-azure-setup.md`.  
**Verified:** `dotnet build` clean; **423** backend tests passed; TerminalPanel unit tests passed.

## Open Questions / Next Steps

1. Push executor image (`Deploy Executor` workflow).
2. Create session pool + grant **Azure ContainerApps Session Executor** to `mi-codesmith-backend-prod-centralus-001` (runbook).
3. Set `CodeExecution__Backend=DynamicSessions` and pool endpoint on `ca-codesmith-api-001`.
4. Align API ingress request timeout with 120s sandbox HTTP timeout if needed.
5. Live smoke: Test Code per language; measure cold vs warm latency.

## Artifacts

| Path | State |
|------|--------|
| `CodeSmith.Core/Models/CodeExecutionRequest.cs` | Complete |
| `CodeSmith.Infrastructure/Services/DynamicSessions/*` | Complete |
| `CodeSmith.Executor/` (+ `Dockerfile`) | Complete |
| `.github/workflows/deploy-executor.yml` | Complete |
| `Docs/general/dynamic-sessions-azure-setup.md` | Complete (ops runbook) |
| `appsettings.json` → `CodeExecution:DynamicSessions` | Complete (empty endpoint locally) |
